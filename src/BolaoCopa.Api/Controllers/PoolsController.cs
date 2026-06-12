using System.Security.Claims;
using BolaoCopa.Application.DTOs;
using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using BolaoCopa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BolaoCopa.Api.Controllers;

[ApiController]
[Route("api/pools")]
[Authorize]
public class PoolsController : ControllerBase
{
    private readonly IPoolRepository _pools;
    private readonly IFeedRepository _feed;
    private readonly IRankingSnapshotRepository _snapshots;
    private readonly IPredictionRepository _predictions;
    private readonly IMatchRepository _matches;

    public PoolsController(IPoolRepository pools, IFeedRepository feed, IRankingSnapshotRepository snapshots, IPredictionRepository predictions, IMatchRepository matches)
    {
        _pools = pools;
        _feed = feed;
        _snapshots = snapshots;
        _predictions = predictions;
        _matches = matches;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyPools()
    {
        var userId = GetUserId();
        var pools = await _pools.GetByUserIdAsync(userId);
        return Ok(pools);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePoolRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Nome do bolão é obrigatório.");

        var userId = GetUserId();
        var pool = new Pool
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsPrivate = request.IsPrivate,
            OwnerUserId = userId
        };

        await _pools.AddAsync(pool);

        await _pools.AddParticipantAsync(new PoolParticipant
        {
            Id = Guid.NewGuid(),
            PoolId = pool.Id,
            UserId = userId
        });

        return Ok(new PoolDto(pool.Id, pool.Name, pool.Description, pool.IsPrivate, pool.InviteCode, 1, pool.OwnerUserId));
    }

    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinPoolRequest request)
    {
        var pool = await _pools.GetByInviteCodeAsync(request.InviteCode);
        if (pool is null) return NotFound("Código de convite inválido.");

        var userId = GetUserId();
        if (await _pools.IsParticipantAsync(pool.Id, userId))
            return Conflict("Você já participa deste bolão.");

        await _pools.AddParticipantAsync(new PoolParticipant
        {
            Id = Guid.NewGuid(),
            PoolId = pool.Id,
            UserId = userId
        });

        // Copy the user's existing predictions (latest per match) into the new pool
        var allPredictions = await _predictions.GetByUserAsync(userId);
        if (allPredictions.Count > 0)
        {
            var latestPerMatch = allPredictions
                .GroupBy(p => p.MatchId)
                .Select(g => g.OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt).First())
                .Select(p => new Prediction
                {
                    Id = Guid.NewGuid(),
                    PoolId = pool.Id,
                    UserId = userId,
                    MatchId = p.MatchId,
                    HomeScorePrediction = p.HomeScorePrediction,
                    AwayScorePrediction = p.AwayScorePrediction
                })
                .ToList();

            if (latestPerMatch.Count > 0)
                await _predictions.BulkUpsertAsync(latestPerMatch, new List<Prediction>());
        }

        return Ok(new { pool.Id, pool.Name, pool.InviteCode });
    }

    [HttpDelete("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id)
    {
        var userId = GetUserId();
        var pool = await _pools.GetByIdAsync(id);
        if (pool is null) return NotFound("Bolão não encontrado.");

        if (pool.OwnerUserId == userId)
            return BadRequest("O dono do bolão não pode sair. Transfira a administração ou exclua o bolão.");

        if (!await _pools.IsParticipantAsync(id, userId))
            return NotFound("Você não é participante deste bolão.");

        await _pools.RemoveParticipantAsync(id, userId);
        return Ok(new { message = "Você saiu do bolão." });
    }

    [HttpGet("{id:guid}/ranking")]
    public async Task<IActionResult> GetRanking(Guid id)
    {
        if (!await _pools.IsParticipantAsync(id, GetUserId())) return Forbid();
        var ranking = await _pools.GetRankingAsync(id);
        return Ok(ranking);
    }

    [HttpGet("{id:guid}/feed")]
    public async Task<IActionResult> GetFeed(Guid id)
    {
        if (!await _pools.IsParticipantAsync(id, GetUserId())) return Forbid();
        var events = await _feed.GetByPoolIdAsync(id, 30);

        // Carrega palpites e jogos do feed para mostrar "palpite vs resultado" e
        // reclassificar o selo na leitura (corrige eventos antigos rotulados errado).
        var matchIds = events.Select(e => e.MatchId).Distinct().ToList();
        var predByUserMatch = new Dictionary<(Guid UserId, Guid MatchId), Prediction>();
        foreach (var matchId in matchIds)
            foreach (var p in await _predictions.GetByMatchAndPoolAsync(matchId, id))
                predByUserMatch[(p.UserId, matchId)] = p;
        var matchById = (await _matches.GetByIdsAsync(matchIds)).ToDictionary(m => m.Id);

        var result = events.Select(e =>
        {
            predByUserMatch.TryGetValue((e.UserId, e.MatchId), out var pr);
            var predLabel = pr is null ? "" : $"{pr.HomeScorePrediction}×{pr.AwayScorePrediction}";

            var eventType = e.EventType;
            if (pr is not null && matchById.TryGetValue(e.MatchId, out var m))
                eventType = ClassifyFeedEvent(pr, m, e.Points);

            return new FeedEventDto(
                e.Id,
                e.UserName,
                e.MatchLabel,
                eventType,
                EventTypeName(eventType),
                e.Points,
                e.OccurredAt,
                predLabel);
        });
        return Ok(result);
    }

    [HttpGet("{id:guid}/ranking-evolution")]
    public async Task<IActionResult> GetRankingEvolution(Guid id)
    {
        var userId = GetUserId();
        if (!await _pools.IsParticipantAsync(id, userId)) return Forbid();
        var snapshots = await _snapshots.GetByPoolAndUserAsync(id, userId);

        // Round display order
        var roundOrder = new[] { "GF", "R32", "R16", "QF", "SF", "3°", "FIN" };

        // Last snapshot per round, then ordered by stage progression
        var result = snapshots
            .GroupBy(s => s.Round)
            .Select(g => g.OrderByDescending(s => s.SnappedAt).First())
            .OrderBy(s => Array.IndexOf(roundOrder, s.Round))
            .Select(s => new RankingEvolutionDto(s.Round, s.TotalPoints));

        return Ok(result);
    }

    [HttpGet("{id:guid}/scoring-config")]
    public async Task<IActionResult> GetScoringConfig(Guid id)
    {
        if (!await _pools.IsParticipantAsync(id, GetUserId())) return Forbid();
        var pool = await _pools.GetByIdAsync(id);
        if (pool is null) return NotFound();
        return Ok(new ScoringConfigDto(
            pool.PointsExactScore, pool.PointsCorrectResult,
            pool.PointsChampion, pool.PointsRunnerUp,
            pool.PointsThirdPlace, pool.PointsGroupQualifier));
    }

    [HttpPut("{id:guid}/scoring-config")]
    public async Task<IActionResult> UpdateScoringConfig(Guid id, [FromBody] ScoringConfigDto dto)
    {
        var pool = await _pools.GetByIdAsync(id);
        if (pool is null) return NotFound();

        var userId = GetUserId();
        if (pool.OwnerUserId != userId)
            return Forbid();

        pool.PointsExactScore    = Math.Clamp(dto.PointsExactScore,    0, 100);
        pool.PointsCorrectResult = Math.Clamp(dto.PointsCorrectResult, 0, 100);
        pool.PointsChampion      = Math.Clamp(dto.PointsChampion,      0, 100);
        pool.PointsRunnerUp      = Math.Clamp(dto.PointsRunnerUp,      0, 100);
        pool.PointsThirdPlace    = Math.Clamp(dto.PointsThirdPlace,    0, 100);
        pool.PointsGroupQualifier = Math.Clamp(dto.PointsGroupQualifier, 0, 100);

        await _pools.UpdateAsync(pool);
        return Ok(new { message = "Configuração de pontuação salva." });
    }

    private static string EventTypeName(int type) => type switch
    {
        1 => "Placar Exato",
        2 => "Resultado Correto",
        3 => "Sem Pontos",
        4 => "Parcial",
        _ => "?"
    };

    // Reclassifica o evento comparando palpite x resultado real (independe do que foi salvo):
    // exato → 1; acertou o vencedor/empate → 2; pontuou sem acertar o vencedor → 4 (parcial); zero → 3.
    private static int ClassifyFeedEvent(Prediction p, Match m, int points)
    {
        if (!m.IsFinished || m.HomeScore is null || m.AwayScore is null) return 3;
        if (p.HomeScorePrediction == m.HomeScore && p.AwayScorePrediction == m.AwayScore)
            return (int)FeedEventType.ExactScore;

        var predResult = p.HomeScorePrediction.CompareTo(p.AwayScorePrediction);
        var realResult = m.HomeScore.Value.CompareTo(m.AwayScore.Value);
        if (predResult == realResult) return (int)FeedEventType.CorrectResult;

        return points > 0 ? (int)FeedEventType.Partial : (int)FeedEventType.ZeroPoints;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public record JoinPoolRequest(string InviteCode);

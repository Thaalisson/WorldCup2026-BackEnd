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

    public PoolsController(IPoolRepository pools, IFeedRepository feed, IRankingSnapshotRepository snapshots)
    {
        _pools = pools;
        _feed = feed;
        _snapshots = snapshots;
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

        return Ok(new PoolDto(pool.Id, pool.Name, pool.Description, pool.IsPrivate, pool.InviteCode, 1));
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

        return Ok(new { pool.Id, pool.Name, pool.InviteCode });
    }

    [HttpGet("{id:guid}/ranking")]
    public async Task<IActionResult> GetRanking(Guid id)
    {
        var ranking = await _pools.GetRankingAsync(id);
        return Ok(ranking);
    }

    [HttpGet("{id:guid}/feed")]
    public async Task<IActionResult> GetFeed(Guid id)
    {
        var events = await _feed.GetByPoolIdAsync(id, 30);
        var result = events.Select(e => new FeedEventDto(
            e.Id,
            e.UserName,
            e.MatchLabel,
            e.EventType,
            EventTypeName(e.EventType),
            e.Points,
            e.OccurredAt));
        return Ok(result);
    }

    [HttpGet("{id:guid}/ranking-evolution")]
    public async Task<IActionResult> GetRankingEvolution(Guid id)
    {
        var userId = GetUserId();
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
        _ => "?"
    };

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public record JoinPoolRequest(string InviteCode);

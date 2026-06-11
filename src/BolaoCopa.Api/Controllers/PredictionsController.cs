using System.Security.Claims;
using BolaoCopa.Application.DTOs;
using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BolaoCopa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PredictionsController : ControllerBase
{
    private readonly IPredictionRepository _predictionRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IScoringService _scoringService;
    private readonly IPoolRepository _pools;

    public PredictionsController(
        IPredictionRepository predictionRepository,
        IMatchRepository matchRepository,
        IScoringService scoringService,
        IPoolRepository pools)
    {
        _predictionRepository = predictionRepository;
        _matchRepository = matchRepository;
        _scoringService = scoringService;
        _pools = pools;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyPredictions([FromQuery] Guid poolId)
    {
        var userId = GetUserId();
        if (!await _pools.IsParticipantAsync(poolId, userId)) return Forbid();

        var predictions = await _predictionRepository.GetByUserAndPoolAsync(userId, poolId);

        var result = predictions.Select(p => new PredictionDto(
            p.Id,
            p.MatchId,
            p.HomeScorePrediction,
            p.AwayScorePrediction,
            p.PointsEarned,
            p.Status.ToString()
        ));

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrUpdate([FromBody] CreatePredictionRequest request)
    {
        var userId = GetUserId();
        if (!await _pools.IsParticipantAsync(request.PoolId, userId)) return Forbid();

        var match = await _matchRepository.GetByIdAsync(request.MatchId);
        if (match is null) return NotFound("Jogo não encontrado.");
        if (match.KickoffAt.AddHours(-1) <= DateTime.UtcNow) return BadRequest("Palpite bloqueado — menos de 1h para o jogo.");

        var existing = await _predictionRepository.GetByUserPoolAndMatchAsync(userId, request.PoolId, request.MatchId);

        if (existing is null)
        {
            var prediction = new Prediction
            {
                Id = Guid.NewGuid(),
                PoolId = request.PoolId,
                UserId = userId,
                MatchId = request.MatchId,
                HomeScorePrediction = request.HomeScorePrediction,
                AwayScorePrediction = request.AwayScorePrediction
            };
            await _predictionRepository.AddAsync(prediction);
            return Ok(prediction);
        }

        existing.HomeScorePrediction = request.HomeScorePrediction;
        existing.AwayScorePrediction = request.AwayScorePrediction;
        existing.UpdatedAt = DateTime.UtcNow;
        await _predictionRepository.UpdateAsync(existing);

        return Ok(existing);
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> BulkCreateOrUpdate([FromBody] BulkPredictionRequest request)
    {
        if (request.Predictions.Count == 0)
            return BadRequest("Nenhum palpite enviado.");

        var userId = GetUserId();
        if (!await _pools.IsParticipantAsync(request.PoolId, userId)) return Forbid();

        var now = DateTime.UtcNow;
        var lockCutoff = now.AddHours(1);

        var matchIds = request.Predictions.Select(p => p.MatchId).Distinct().ToList();
        var matches = await _matchRepository.GetByIdsAsync(matchIds);
        var matchMap = matches.ToDictionary(m => m.Id);

        var existingPredictions = await _predictionRepository.GetByUserAndPoolAsync(userId, request.PoolId);
        var existingMap = existingPredictions.ToDictionary(p => p.MatchId);

        var toAdd = new List<Prediction>();
        var toUpdate = new List<Prediction>();
        var skipped = new List<Guid>();

        foreach (var item in request.Predictions)
        {
            if (!matchMap.TryGetValue(item.MatchId, out var match) || match.KickoffAt <= lockCutoff)
            {
                skipped.Add(item.MatchId);
                continue;
            }

            if (existingMap.TryGetValue(item.MatchId, out var existing))
            {
                existing.HomeScorePrediction = item.HomeScorePrediction;
                existing.AwayScorePrediction = item.AwayScorePrediction;
                existing.UpdatedAt = now;
                toUpdate.Add(existing);
            }
            else
            {
                toAdd.Add(new Prediction
                {
                    Id = Guid.NewGuid(),
                    PoolId = request.PoolId,
                    UserId = userId,
                    MatchId = item.MatchId,
                    HomeScorePrediction = item.HomeScorePrediction,
                    AwayScorePrediction = item.AwayScorePrediction,
                });
            }
        }

        var saved = await _predictionRepository.BulkUpsertAsync(toAdd, toUpdate);
        return Ok(new { saved, skipped = skipped.Count });
    }

    [HttpPost("replicate")]
    public async Task<IActionResult> ReplicateToAllPools([FromBody] ReplicatePredictionsRequest request)
    {
        var userId = GetUserId();
        if (!await _pools.IsParticipantAsync(request.SourcePoolId, userId)) return Forbid();

        var userPools = await _pools.GetByUserIdAsync(userId);
        var targetPoolIds = userPools
            .Select(p => p.Id)
            .Where(id => id != request.SourcePoolId)
            .ToList();

        if (targetPoolIds.Count == 0)
            return Ok(new { replicated = 0, pools = 0 });

        var now = DateTime.UtcNow;
        var lockCutoff = now.AddHours(1);

        var sourcePredictions = await _predictionRepository.GetByUserAndPoolAsync(userId, request.SourcePoolId);
        var matchIds = sourcePredictions.Select(p => p.MatchId).Distinct().ToList();
        var matches = await _matchRepository.GetByIdsAsync(matchIds);
        var matchMap = matches.ToDictionary(m => m.Id);

        var replicable = sourcePredictions
            .Where(p => matchMap.TryGetValue(p.MatchId, out var m) && m.KickoffAt > lockCutoff)
            .ToList();

        if (replicable.Count == 0)
            return Ok(new { replicated = 0, pools = targetPoolIds.Count });

        int totalReplicated = 0;
        foreach (var targetPoolId in targetPoolIds)
        {
            var existing = await _predictionRepository.GetByUserAndPoolAsync(userId, targetPoolId);
            var existingMap = existing.ToDictionary(p => p.MatchId);

            var toAdd = new List<Prediction>();
            var toUpdate = new List<Prediction>();

            foreach (var src in replicable)
            {
                if (existingMap.TryGetValue(src.MatchId, out var ex))
                {
                    ex.HomeScorePrediction = src.HomeScorePrediction;
                    ex.AwayScorePrediction = src.AwayScorePrediction;
                    ex.UpdatedAt = now;
                    toUpdate.Add(ex);
                }
                else
                {
                    toAdd.Add(new Prediction
                    {
                        Id = Guid.NewGuid(),
                        PoolId = targetPoolId,
                        UserId = userId,
                        MatchId = src.MatchId,
                        HomeScorePrediction = src.HomeScorePrediction,
                        AwayScorePrediction = src.AwayScorePrediction,
                    });
                }
            }

            totalReplicated += await _predictionRepository.BulkUpsertAsync(toAdd, toUpdate);
        }

        return Ok(new { replicated = totalReplicated, pools = targetPoolIds.Count });
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public record ReplicatePredictionsRequest(Guid SourcePoolId);

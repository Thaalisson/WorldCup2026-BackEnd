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

    public PredictionsController(
        IPredictionRepository predictionRepository,
        IMatchRepository matchRepository,
        IScoringService scoringService)
    {
        _predictionRepository = predictionRepository;
        _matchRepository = matchRepository;
        _scoringService = scoringService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyPredictions([FromQuery] Guid poolId)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
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
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var match = await _matchRepository.GetByIdAsync(request.MatchId);
        if (match is null) return NotFound("Jogo não encontrado.");
        if (match.KickoffAt <= DateTime.UtcNow) return BadRequest("Palpite bloqueado — jogo já começou.");

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

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var now = DateTime.UtcNow;

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
            if (!matchMap.TryGetValue(item.MatchId, out var match) || match.KickoffAt <= now)
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
}

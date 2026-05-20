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
}

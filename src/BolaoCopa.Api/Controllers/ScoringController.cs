using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace BolaoCopa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScoringController : ControllerBase
{
    private readonly IScoringService _scoringService;

    public ScoringController(IScoringService scoringService)
    {
        _scoringService = scoringService;
    }

    [HttpPost("preview")]
    public IActionResult Preview([FromBody] ScoringPreviewRequest request)
    {
        var prediction = new Prediction
        {
            HomeScorePrediction = request.PredictedHome,
            AwayScorePrediction = request.PredictedAway
        };

        var match = new Match
        {
            HomeScore = request.RealHome,
            AwayScore = request.RealAway,
            IsFinished = true
        };

        return Ok(new { points = _scoringService.CalculateMatchPredictionPoints(prediction, match) });
    }
}

public record ScoringPreviewRequest(int PredictedHome, int PredictedAway, int RealHome, int RealAway);

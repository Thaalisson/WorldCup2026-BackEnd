namespace BolaoCopa.Application.DTOs;

public record PredictionDto(
    Guid Id,
    Guid MatchId,
    int HomeScorePrediction,
    int AwayScorePrediction,
    int PointsEarned,
    string Status
);

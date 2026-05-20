namespace BolaoCopa.Application.DTOs;

public record CreatePredictionRequest(Guid PoolId, Guid MatchId, int HomeScorePrediction, int AwayScorePrediction);

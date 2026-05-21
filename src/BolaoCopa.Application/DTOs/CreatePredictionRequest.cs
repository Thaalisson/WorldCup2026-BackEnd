namespace BolaoCopa.Application.DTOs;

public record CreatePredictionRequest(Guid PoolId, Guid MatchId, int HomeScorePrediction, int AwayScorePrediction);

public record BulkPredictionItem(Guid MatchId, int HomeScorePrediction, int AwayScorePrediction);

public record BulkPredictionRequest(Guid PoolId, List<BulkPredictionItem> Predictions);

using BolaoCopa.Domain.Enums;

namespace BolaoCopa.Domain.Entities;

public class Prediction
{
    public Guid Id { get; set; }
    public Guid PoolId { get; set; }
    public Guid UserId { get; set; }
    public Guid MatchId { get; set; }
    public int HomeScorePrediction { get; set; }
    public int AwayScorePrediction { get; set; }
    public int PointsEarned { get; set; }
    public PredictionStatus Status { get; set; } = PredictionStatus.Saved;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

namespace BolaoCopa.Domain.Entities;

public class PoolParticipant
{
    public Guid Id { get; set; }
    public Guid PoolId { get; set; }
    public Guid UserId { get; set; }
    public int TotalPoints { get; set; }
    public int ExactScores { get; set; }
    public int CorrectResults { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

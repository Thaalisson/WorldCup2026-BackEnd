namespace BolaoCopa.Domain.Entities;

public class RankingSnapshot
{
    public Guid Id { get; set; }
    public Guid PoolId { get; set; }
    public Guid UserId { get; set; }
    public string Round { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public DateTime SnappedAt { get; set; } = DateTime.UtcNow;
}

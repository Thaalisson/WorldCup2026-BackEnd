namespace BolaoCopa.Domain.Entities;

public class FeedEvent
{
    public Guid Id { get; set; }
    public Guid PoolId { get; set; }
    public Guid UserId { get; set; }
    public Guid MatchId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string MatchLabel { get; set; } = string.Empty;
    public int EventType { get; set; }
    public int Points { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

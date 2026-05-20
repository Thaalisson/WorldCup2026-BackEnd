namespace BolaoCopa.Domain.Entities;

public class Pool
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerUserId { get; set; }
    public bool IsPrivate { get; set; }
    public string InviteCode { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Scoring config — pool owner can customize
    public int PointsExactScore { get; set; } = 10;
    public int PointsCorrectResult { get; set; } = 5;
    public int PointsChampion { get; set; } = 15;
    public int PointsRunnerUp { get; set; } = 10;
    public int PointsThirdPlace { get; set; } = 5;
    public int PointsGroupQualifier { get; set; } = 3;
}

namespace BolaoCopa.Domain.Entities;

public class GroupPrediction
{
    public Guid Id { get; set; }
    public Guid PoolId { get; set; }
    public Guid UserId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public Guid FirstPlaceTeamId { get; set; }
    public Guid SecondPlaceTeamId { get; set; }
    public int PointsEarned { get; set; }
}

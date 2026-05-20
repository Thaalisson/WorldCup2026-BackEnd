namespace BolaoCopa.Domain.Entities;

public class ChampionPrediction
{
    public Guid Id { get; set; }
    public Guid PoolId { get; set; }
    public Guid UserId { get; set; }
    public Guid ChampionTeamId { get; set; }
    public Guid? RunnerUpTeamId { get; set; }
    public Guid? ThirdPlaceTeamId { get; set; }
    public Guid? TopScorerPlayerId { get; set; }
    public Guid? MvpPlayerId { get; set; }
    public int PointsEarned { get; set; }
}

using BolaoCopa.Domain.Enums;

namespace BolaoCopa.Domain.Entities;

public class Match
{
    public Guid Id { get; set; }
    public string FifaMatchCode { get; set; } = string.Empty;
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
    public DateTime KickoffAt { get; set; }
    public TournamentStage Stage { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.Scheduled;
    public string? GroupName { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public bool IsFinished { get; set; }
    public int? ApiFootballFixtureId { get; set; }
}

namespace BolaoCopa.Application.DTOs;

public record TeamSummaryDto(Guid Id, string Name, string Code, string? IsoCode, string? GroupName);

public record MatchWithTeamsDto(
    Guid Id,
    TeamSummaryDto HomeTeam,
    TeamSummaryDto AwayTeam,
    DateTime KickoffAt,
    string Stage,
    string? GroupName,
    int? HomeScore,
    int? AwayScore,
    bool IsFinished
);

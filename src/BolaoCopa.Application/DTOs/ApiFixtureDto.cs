namespace BolaoCopa.Application.DTOs;

public record ApiFixtureDto(
    int FixtureId,
    DateTime Date,
    string StatusShort,
    string Round,
    int HomeTeamId,
    string HomeTeamName,
    int AwayTeamId,
    string AwayTeamName,
    int? HomeGoals,
    int? AwayGoals,
    string HomeTeamCode = "",
    string AwayTeamCode = ""
);

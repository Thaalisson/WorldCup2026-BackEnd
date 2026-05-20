namespace BolaoCopa.Application.DTOs;

public record ChampionPredictionDto(
    Guid Id,
    Guid PoolId,
    Guid ChampionTeamId,
    string ChampionTeamName,
    string? ChampionTeamIsoCode,
    Guid? RunnerUpTeamId,
    string? RunnerUpTeamName,
    string? RunnerUpTeamIsoCode,
    Guid? ThirdPlaceTeamId,
    string? ThirdPlaceTeamName,
    string? ThirdPlaceTeamIsoCode,
    int PointsEarned
);

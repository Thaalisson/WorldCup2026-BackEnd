namespace BolaoCopa.Application.DTOs;

public record SaveChampionPredictionRequest(
    Guid PoolId,
    Guid ChampionTeamId,
    Guid? RunnerUpTeamId,
    Guid? ThirdPlaceTeamId
);

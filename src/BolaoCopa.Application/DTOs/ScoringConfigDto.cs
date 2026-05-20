namespace BolaoCopa.Application.DTOs;

public record ScoringConfigDto(
    int PointsExactScore,
    int PointsCorrectResult,
    int PointsChampion,
    int PointsRunnerUp,
    int PointsThirdPlace,
    int PointsGroupQualifier
);

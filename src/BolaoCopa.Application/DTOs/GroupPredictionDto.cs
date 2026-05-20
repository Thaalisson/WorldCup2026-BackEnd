namespace BolaoCopa.Application.DTOs;

public record GroupPredictionDto(
    Guid Id,
    string GroupName,
    string FirstPlaceTeamId,
    string FirstPlaceTeamName,
    string? FirstPlaceIsoCode,
    string SecondPlaceTeamId,
    string SecondPlaceTeamName,
    string? SecondPlaceIsoCode,
    int PointsEarned
);

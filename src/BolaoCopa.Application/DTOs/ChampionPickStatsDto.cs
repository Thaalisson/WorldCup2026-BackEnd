namespace BolaoCopa.Application.DTOs;

public record ChampionPickStatsDto(
    string TeamId,
    string TeamName,
    string? IsoCode,
    int Count
);

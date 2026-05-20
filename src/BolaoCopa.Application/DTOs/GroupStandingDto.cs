namespace BolaoCopa.Application.DTOs;

public record GroupStandingDto(
    string TeamId,
    string TeamName,
    string? IsoCode,
    int Points,
    int Played,
    int Wins,
    int Draws,
    int Losses,
    int GoalsFor,
    int GoalsAgainst,
    int GoalDiff
);

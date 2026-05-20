namespace BolaoCopa.Application.DTOs;

public record RankingItemDto(
    Guid UserId,
    string UserName,
    int Position,
    int TotalPoints,
    int ExactScores,
    int CorrectResults
);

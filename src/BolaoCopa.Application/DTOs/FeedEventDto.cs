namespace BolaoCopa.Application.DTOs;

public record FeedEventDto(
    Guid Id,
    string UserName,
    string MatchLabel,
    int EventType,
    string EventTypeName,
    int Points,
    DateTime OccurredAt,
    string PredictionLabel = ""
);

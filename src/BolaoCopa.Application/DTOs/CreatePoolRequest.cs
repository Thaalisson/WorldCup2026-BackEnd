namespace BolaoCopa.Application.DTOs;

public record CreatePoolRequest(string Name, string? Description, bool IsPrivate);

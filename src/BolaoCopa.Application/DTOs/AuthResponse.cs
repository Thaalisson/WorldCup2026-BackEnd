namespace BolaoCopa.Application.DTOs;

public record AuthResponse(string Token, Guid UserId, string Name, string Email, bool IsAdmin);

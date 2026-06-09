namespace BolaoCopa.Application.DTOs;

public record PoolDto(Guid Id, string Name, string? Description, bool IsPrivate, string InviteCode, int ParticipantCount, Guid OwnerUserId);

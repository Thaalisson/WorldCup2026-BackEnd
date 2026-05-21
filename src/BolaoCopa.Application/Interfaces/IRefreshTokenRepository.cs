using BolaoCopa.Domain.Entities;

namespace BolaoCopa.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task AddAsync(RefreshToken refreshToken);
    Task RevokeAsync(RefreshToken refreshToken);
    Task RevokeAllForUserAsync(Guid userId);
}

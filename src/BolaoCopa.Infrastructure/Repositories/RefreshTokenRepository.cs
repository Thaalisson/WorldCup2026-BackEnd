using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using BolaoCopa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _db;

    public RefreshTokenRepository(AppDbContext db) => _db = db;

    public Task<RefreshToken?> GetByTokenAsync(string token) =>
        _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token);

    public async Task AddAsync(RefreshToken refreshToken)
    {
        await _db.RefreshTokens.AddAsync(refreshToken);
        await _db.SaveChangesAsync();
    }

    public async Task RevokeAsync(RefreshToken refreshToken)
    {
        refreshToken.IsRevoked = true;
        await _db.SaveChangesAsync();
    }

    public async Task RevokeAllForUserAsync(Guid userId)
    {
        await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true));
    }
}

using BolaoCopa.Application.DTOs;
using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using BolaoCopa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa.Infrastructure.Repositories;

public class PoolRepository : IPoolRepository
{
    private readonly AppDbContext _context;

    public PoolRepository(AppDbContext context) => _context = context;

    public Task<Pool?> GetByIdAsync(Guid id) =>
        _context.Pools.FindAsync(id).AsTask();

    public Task<Pool?> GetByInviteCodeAsync(string code) =>
        _context.Pools.FirstOrDefaultAsync(p => p.InviteCode == code.ToUpperInvariant());

    public async Task<IReadOnlyList<PoolDto>> GetByUserIdAsync(Guid userId)
    {
        return await (
            from pp in _context.PoolParticipants
            join p in _context.Pools on pp.PoolId equals p.Id
            where pp.UserId == userId
            select new PoolDto(
                p.Id,
                p.Name,
                p.Description,
                p.IsPrivate,
                p.InviteCode,
                _context.PoolParticipants.Count(x => x.PoolId == p.Id)
            )
        ).ToListAsync();
    }

    public async Task AddAsync(Pool pool)
    {
        await _context.Pools.AddAsync(pool);
        await _context.SaveChangesAsync();
    }

    public Task<bool> IsParticipantAsync(Guid poolId, Guid userId) =>
        _context.PoolParticipants.AnyAsync(p => p.PoolId == poolId && p.UserId == userId);

    public async Task AddParticipantAsync(PoolParticipant participant)
    {
        await _context.PoolParticipants.AddAsync(participant);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<RankingItemDto>> GetRankingAsync(Guid poolId)
    {
        var rows = await (
            from pp in _context.PoolParticipants
            join u in _context.Users on pp.UserId equals u.Id
            where pp.PoolId == poolId
            orderby pp.TotalPoints descending, pp.ExactScores descending
            select new { pp.UserId, u.Name, pp.TotalPoints, pp.ExactScores, pp.CorrectResults }
        ).ToListAsync();

        return rows
            .Select((r, i) => new RankingItemDto(r.UserId, r.Name, i + 1, r.TotalPoints, r.ExactScores, r.CorrectResults))
            .ToList();
    }

    public Task<PoolParticipant?> GetParticipantAsync(Guid poolId, Guid userId) =>
        _context.PoolParticipants.FirstOrDefaultAsync(p => p.PoolId == poolId && p.UserId == userId);

    public async Task<IReadOnlyList<PoolParticipant>> GetParticipantsAsync(Guid poolId) =>
        await _context.PoolParticipants.Where(p => p.PoolId == poolId).ToListAsync();

    public async Task UpdateParticipantAsync(PoolParticipant participant)
    {
        _context.PoolParticipants.Update(participant);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Pool pool)
    {
        _context.Pools.Update(pool);
        await _context.SaveChangesAsync();
    }
}

using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using BolaoCopa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa.Infrastructure.Repositories;

public class RankingSnapshotRepository : IRankingSnapshotRepository
{
    private readonly AppDbContext _context;

    public RankingSnapshotRepository(AppDbContext context) => _context = context;

    public async Task AddRangeAsync(IEnumerable<RankingSnapshot> snapshots)
    {
        await _context.RankingSnapshots.AddRangeAsync(snapshots);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<RankingSnapshot>> GetByPoolAndUserAsync(Guid poolId, Guid userId)
    {
        return await _context.RankingSnapshots
            .Where(x => x.PoolId == poolId && x.UserId == userId)
            .OrderBy(x => x.SnappedAt)
            .ToListAsync();
    }
}

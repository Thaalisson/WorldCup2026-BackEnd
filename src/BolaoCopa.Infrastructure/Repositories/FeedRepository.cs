using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using BolaoCopa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa.Infrastructure.Repositories;

public class FeedRepository : IFeedRepository
{
    private readonly AppDbContext _context;

    public FeedRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(FeedEvent evt)
    {
        await _context.FeedEvents.AddAsync(evt);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<FeedEvent>> GetByPoolIdAsync(Guid poolId, int limit = 30)
    {
        return await _context.FeedEvents
            .Where(x => x.PoolId == poolId)
            .OrderByDescending(x => x.OccurredAt)
            .Take(limit)
            .ToListAsync();
    }
}

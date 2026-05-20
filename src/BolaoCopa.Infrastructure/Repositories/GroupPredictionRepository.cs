using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using BolaoCopa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa.Infrastructure.Repositories;

public class GroupPredictionRepository : IGroupPredictionRepository
{
    private readonly AppDbContext _context;

    public GroupPredictionRepository(AppDbContext context) => _context = context;

    public Task<GroupPrediction?> GetByUserPoolAndGroupAsync(Guid userId, Guid poolId, string groupName) =>
        _context.GroupPredictions.FirstOrDefaultAsync(x =>
            x.UserId == userId && x.PoolId == poolId && x.GroupName == groupName);

    public async Task<IReadOnlyList<GroupPrediction>> GetByUserAndPoolAsync(Guid userId, Guid poolId) =>
        await _context.GroupPredictions
            .Where(x => x.UserId == userId && x.PoolId == poolId)
            .OrderBy(x => x.GroupName)
            .ToListAsync();

    public async Task<IReadOnlyList<GroupPrediction>> GetByPoolAsync(Guid poolId) =>
        await _context.GroupPredictions
            .Where(x => x.PoolId == poolId)
            .ToListAsync();

    public async Task AddAsync(GroupPrediction prediction)
    {
        await _context.GroupPredictions.AddAsync(prediction);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(GroupPrediction prediction)
    {
        _context.GroupPredictions.Update(prediction);
        await _context.SaveChangesAsync();
    }
}

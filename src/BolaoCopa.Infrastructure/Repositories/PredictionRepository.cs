using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using BolaoCopa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa.Infrastructure.Repositories;

public class PredictionRepository : IPredictionRepository
{
    private readonly AppDbContext _context;

    public PredictionRepository(AppDbContext context) => _context = context;

    public Task<Prediction?> GetByUserPoolAndMatchAsync(Guid userId, Guid poolId, Guid matchId) =>
        _context.Predictions.FirstOrDefaultAsync(x => x.UserId == userId && x.PoolId == poolId && x.MatchId == matchId);

    public async Task<IReadOnlyList<Prediction>> GetByMatchIdAsync(Guid matchId) =>
        await _context.Predictions.Where(x => x.MatchId == matchId).ToListAsync();

    public async Task<IReadOnlyList<Prediction>> GetByUserAndPoolAsync(Guid userId, Guid poolId) =>
        await _context.Predictions.Where(x => x.UserId == userId && x.PoolId == poolId).ToListAsync();

    public async Task<IReadOnlyList<Prediction>> GetByMatchAndPoolAsync(Guid matchId, Guid poolId) =>
        await _context.Predictions.Where(x => x.MatchId == matchId && x.PoolId == poolId).ToListAsync();

    public async Task AddAsync(Prediction prediction)
    {
        await _context.Predictions.AddAsync(prediction);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Prediction prediction)
    {
        _context.Predictions.Update(prediction);
        await _context.SaveChangesAsync();
    }

    public async Task<int> BulkUpsertAsync(IReadOnlyList<Prediction> toAdd, IReadOnlyList<Prediction> toUpdate)
    {
        if (toAdd.Count > 0) await _context.Predictions.AddRangeAsync(toAdd);
        if (toUpdate.Count > 0) _context.Predictions.UpdateRange(toUpdate);
        await _context.SaveChangesAsync();
        return toAdd.Count + toUpdate.Count;
    }
}

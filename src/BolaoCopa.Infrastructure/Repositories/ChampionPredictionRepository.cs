using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using BolaoCopa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa.Infrastructure.Repositories;

public class ChampionPredictionRepository : IChampionPredictionRepository
{
    private readonly AppDbContext _context;

    public ChampionPredictionRepository(AppDbContext context) => _context = context;

    public Task<ChampionPrediction?> GetByUserAndPoolAsync(Guid userId, Guid poolId) =>
        _context.ChampionPredictions.FirstOrDefaultAsync(x => x.UserId == userId && x.PoolId == poolId);

    public async Task AddAsync(ChampionPrediction prediction)
    {
        await _context.ChampionPredictions.AddAsync(prediction);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ChampionPrediction prediction)
    {
        _context.ChampionPredictions.Update(prediction);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<(Guid TeamId, int Count)>> GetChampionCountsByPoolAsync(Guid poolId)
    {
        var result = await _context.ChampionPredictions
            .Where(x => x.PoolId == poolId)
            .GroupBy(x => x.ChampionTeamId)
            .Select(g => new { TeamId = g.Key, Count = g.Count() })
            .ToListAsync();

        return result.Select(x => (x.TeamId, x.Count)).ToList();
    }
}

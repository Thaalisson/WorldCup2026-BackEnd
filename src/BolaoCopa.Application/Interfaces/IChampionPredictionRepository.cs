using BolaoCopa.Domain.Entities;

namespace BolaoCopa.Application.Interfaces;

public interface IChampionPredictionRepository
{
    Task<ChampionPrediction?> GetByUserAndPoolAsync(Guid userId, Guid poolId);
    Task AddAsync(ChampionPrediction prediction);
    Task UpdateAsync(ChampionPrediction prediction);
    Task<IReadOnlyList<(Guid TeamId, int Count)>> GetChampionCountsByPoolAsync(Guid poolId);
}

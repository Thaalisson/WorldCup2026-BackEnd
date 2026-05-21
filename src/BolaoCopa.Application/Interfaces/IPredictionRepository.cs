using BolaoCopa.Domain.Entities;

namespace BolaoCopa.Application.Interfaces;

public interface IPredictionRepository
{
    Task<Prediction?> GetByUserPoolAndMatchAsync(Guid userId, Guid poolId, Guid matchId);
    Task<IReadOnlyList<Prediction>> GetByMatchIdAsync(Guid matchId);
    Task<IReadOnlyList<Prediction>> GetByUserAndPoolAsync(Guid userId, Guid poolId);
    Task<IReadOnlyList<Prediction>> GetByMatchAndPoolAsync(Guid matchId, Guid poolId);
    Task AddAsync(Prediction prediction);
    Task UpdateAsync(Prediction prediction);
    Task<int> BulkUpsertAsync(IReadOnlyList<Prediction> toAdd, IReadOnlyList<Prediction> toUpdate);
}

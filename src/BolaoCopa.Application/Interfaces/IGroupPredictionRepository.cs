using BolaoCopa.Domain.Entities;

namespace BolaoCopa.Application.Interfaces;

public interface IGroupPredictionRepository
{
    Task<GroupPrediction?> GetByUserPoolAndGroupAsync(Guid userId, Guid poolId, string groupName);
    Task<IReadOnlyList<GroupPrediction>> GetByUserAndPoolAsync(Guid userId, Guid poolId);
    Task<IReadOnlyList<GroupPrediction>> GetByPoolAsync(Guid poolId);
    Task AddAsync(GroupPrediction prediction);
    Task UpdateAsync(GroupPrediction prediction);
}

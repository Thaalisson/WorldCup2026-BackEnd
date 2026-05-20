using BolaoCopa.Domain.Entities;

namespace BolaoCopa.Application.Interfaces;

public interface IFeedRepository
{
    Task AddAsync(FeedEvent evt);
    Task<IReadOnlyList<FeedEvent>> GetByPoolIdAsync(Guid poolId, int limit = 30);
}

using BolaoCopa.Domain.Entities;

namespace BolaoCopa.Application.Interfaces;

public interface IRankingSnapshotRepository
{
    Task AddRangeAsync(IEnumerable<RankingSnapshot> snapshots);
    Task<IReadOnlyList<RankingSnapshot>> GetByPoolAndUserAsync(Guid poolId, Guid userId);
}

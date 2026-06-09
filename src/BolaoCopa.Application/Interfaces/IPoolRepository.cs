using BolaoCopa.Application.DTOs;
using BolaoCopa.Domain.Entities;

namespace BolaoCopa.Application.Interfaces;

public interface IPoolRepository
{
    Task<Pool?> GetByIdAsync(Guid id);
    Task<Pool?> GetByInviteCodeAsync(string code);
    Task<IReadOnlyList<PoolDto>> GetByUserIdAsync(Guid userId);
    Task AddAsync(Pool pool);
    Task<bool> IsParticipantAsync(Guid poolId, Guid userId);
    Task AddParticipantAsync(PoolParticipant participant);
    Task<IReadOnlyList<RankingItemDto>> GetRankingAsync(Guid poolId);
    Task<PoolParticipant?> GetParticipantAsync(Guid poolId, Guid userId);
    Task<IReadOnlyList<PoolParticipant>> GetParticipantsAsync(Guid poolId);
    Task UpdateParticipantAsync(PoolParticipant participant);
    Task RemoveParticipantAsync(Guid poolId, Guid userId);
    Task UpdateAsync(Pool pool);
}

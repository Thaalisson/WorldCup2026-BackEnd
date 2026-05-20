using BolaoCopa.Domain.Entities;

namespace BolaoCopa.Application.Interfaces;

public interface ITeamRepository
{
    Task<IReadOnlyList<Team>> GetAllAsync();
    Task<Team?> GetByApiTeamIdAsync(int apiTeamId);
    Task AddAsync(Team team);
}

using BolaoCopa.Application.DTOs;
using BolaoCopa.Domain.Entities;

namespace BolaoCopa.Application.Interfaces;

public interface IMatchRepository
{
    Task<IReadOnlyList<MatchWithTeamsDto>> GetAllWithTeamsAsync();
    Task<Match?> GetByIdAsync(Guid id);
    Task<Match?> GetByApiFixtureIdAsync(int apiFixtureId);
    Task<IReadOnlyList<Match>> GetFinishedMatchesAsync();
    Task AddAsync(Match match);
    Task UpdateAsync(Match match);
}

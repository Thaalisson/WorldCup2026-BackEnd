using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using BolaoCopa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa.Infrastructure.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly AppDbContext _context;

    public TeamRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<Team>> GetAllAsync() =>
        await _context.Teams
            .Where(t => t.Name != "A Definir")
            .OrderBy(t => t.GroupName)
            .ThenBy(t => t.Name)
            .ToListAsync();

    public Task<Team?> GetByApiTeamIdAsync(int apiTeamId) =>
        _context.Teams.FirstOrDefaultAsync(t => t.ApiFootballTeamId == apiTeamId);

    public async Task AddAsync(Team team)
    {
        await _context.Teams.AddAsync(team);
        await _context.SaveChangesAsync();
    }
}

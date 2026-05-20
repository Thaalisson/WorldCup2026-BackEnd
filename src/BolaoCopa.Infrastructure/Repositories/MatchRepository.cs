using BolaoCopa.Application.DTOs;
using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using BolaoCopa.Domain.Enums;
using BolaoCopa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa.Infrastructure.Repositories;

public class MatchRepository : IMatchRepository
{
    private readonly AppDbContext _context;

    public MatchRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<MatchWithTeamsDto>> GetAllWithTeamsAsync()
    {
        var rows = await (
            from m in _context.Matches
            join ht in _context.Teams on m.HomeTeamId equals ht.Id
            join at in _context.Teams on m.AwayTeamId equals at.Id
            orderby m.KickoffAt
            select new { m, ht, at }
        ).ToListAsync();

        return rows.Select(r => new MatchWithTeamsDto(
            r.m.Id,
            new TeamSummaryDto(r.ht.Id, r.ht.Name, r.ht.Code, r.ht.IsoCode, r.ht.GroupName),
            new TeamSummaryDto(r.at.Id, r.at.Name, r.at.Code, r.at.IsoCode, r.at.GroupName),
            r.m.KickoffAt,
            StageName(r.m.Stage),
            r.m.GroupName,
            r.m.HomeScore,
            r.m.AwayScore,
            r.m.IsFinished
        )).ToList();
    }

    public Task<Match?> GetByIdAsync(Guid id) =>
        _context.Matches.FindAsync(id).AsTask();

    public async Task<IReadOnlyList<Match>> GetByIdsAsync(IEnumerable<Guid> ids) =>
        await _context.Matches.Where(m => ids.Contains(m.Id)).ToListAsync();

    public Task<Match?> GetByApiFixtureIdAsync(int apiFixtureId) =>
        _context.Matches.FirstOrDefaultAsync(m => m.ApiFootballFixtureId == apiFixtureId);

    public async Task AddAsync(Match match)
    {
        await _context.Matches.AddAsync(match);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Match>> GetFinishedMatchesAsync() =>
        await _context.Matches.Where(m => m.IsFinished).ToListAsync();

    public async Task UpdateAsync(Match match)
    {
        _context.Matches.Update(match);
        await _context.SaveChangesAsync();
    }

    private static string StageName(TournamentStage s) => s switch
    {
        TournamentStage.GroupStage   => "Fase de grupos",
        TournamentStage.RoundOf32    => "Round of 32",
        TournamentStage.RoundOf16    => "Oitavas de final",
        TournamentStage.QuarterFinal => "Quartas de final",
        TournamentStage.SemiFinal    => "Semifinal",
        TournamentStage.ThirdPlace   => "Disputa de 3º lugar",
        TournamentStage.Final        => "Final",
        _                            => "Desconhecido"
    };
}

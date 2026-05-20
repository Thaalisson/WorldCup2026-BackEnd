using System.Text.RegularExpressions;
using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using BolaoCopa.Domain.Enums;
using DomainMatch = BolaoCopa.Domain.Entities.Match;

namespace BolaoCopa.Application.Services;

public class MatchSyncService : IMatchSyncService
{
    private readonly IFootballApiClient _apiClient;
    private readonly IMatchRepository _matches;
    private readonly ITeamRepository _teams;
    private readonly IRankingService _ranking;

    public MatchSyncService(
        IFootballApiClient apiClient,
        IMatchRepository matches,
        ITeamRepository teams,
        IRankingService ranking)
    {
        _apiClient = apiClient;
        _matches = matches;
        _teams = teams;
        _ranking = ranking;
    }

    public async Task<int> ImportFixturesAsync()
    {
        var fixtures = await _apiClient.GetAllFixturesAsync();
        int imported = 0;

        foreach (var f in fixtures)
        {
            var existing = await _matches.GetByApiFixtureIdAsync(f.FixtureId);
            if (existing is not null) continue;

            var homeTeam = await EnsureTeamAsync(f.HomeTeamId, f.HomeTeamName);
            var awayTeam = await EnsureTeamAsync(f.AwayTeamId, f.AwayTeamName);

            var match = new DomainMatch
            {
                Id = Guid.NewGuid(),
                FifaMatchCode = f.FixtureId.ToString(),
                HomeTeamId = homeTeam.Id,
                AwayTeamId = awayTeam.Id,
                KickoffAt = f.Date,
                Stage = MapStage(f.Round),
                GroupName = ExtractGroup(f.Round),
                ApiFootballFixtureId = f.FixtureId
            };

            await _matches.AddAsync(match);
            imported++;
        }

        return imported;
    }

    public async Task SyncResultsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var copaStart = new DateOnly(2026, 6, 11);
        var copaEnd = new DateOnly(2026, 7, 20);

        // Fora do período da Copa: não consome quota da API
        if (today < copaStart || today > copaEnd) return;

        var finished = await _apiClient.GetTodayFinishedFixturesAsync();

        foreach (var f in finished)
        {
            if (f.HomeGoals is null || f.AwayGoals is null) continue;

            var match = await _matches.GetByApiFixtureIdAsync(f.FixtureId);
            if (match is null || match.IsFinished) continue;

            match.HomeScore = f.HomeGoals.Value;
            match.AwayScore = f.AwayGoals.Value;
            match.IsFinished = true;
            await _matches.UpdateAsync(match);
            await _ranking.RecalculateForMatchAsync(match);
        }
    }

    private async Task<Team> EnsureTeamAsync(int apiTeamId, string name)
    {
        var team = await _teams.GetByApiTeamIdAsync(apiTeamId);
        if (team is not null) return team;

        team = new Team
        {
            Id = Guid.NewGuid(),
            Name = name,
            Code = name.Length >= 3 ? name[..3].ToUpperInvariant() : name.ToUpperInvariant(),
            ApiFootballTeamId = apiTeamId
        };
        await _teams.AddAsync(team);
        return team;
    }

    private static TournamentStage MapStage(string round) => round.ToLowerInvariant() switch
    {
        var r when r.Contains("group")        => TournamentStage.GroupStage,
        var r when r.Contains("32")           => TournamentStage.RoundOf32,
        var r when r.Contains("16")           => TournamentStage.RoundOf16,
        var r when r.Contains("quarter")      => TournamentStage.QuarterFinal,
        var r when r.Contains("semi")         => TournamentStage.SemiFinal,
        var r when r.Contains("3rd") || r.Contains("third") => TournamentStage.ThirdPlace,
        var r when r.Contains("final")        => TournamentStage.Final,
        _                                     => TournamentStage.GroupStage
    };

    private static string? ExtractGroup(string round)
    {
        var m = Regex.Match(round, @"Group\s+([A-L])\b", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
    }
}

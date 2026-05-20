using BolaoCopa.Application.DTOs;
using BolaoCopa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BolaoCopa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatchesController : ControllerBase
{
    private readonly IMatchRepository _matches;
    private readonly IRankingService _ranking;

    public MatchesController(IMatchRepository matches, IRankingService ranking)
    {
        _matches = matches;
        _ranking = ranking;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var matches = await _matches.GetAllWithTeamsAsync();
        return Ok(matches);
    }

    [HttpGet("group-standings")]
    public async Task<IActionResult> GetGroupStandings()
    {
        var matches = await _matches.GetAllWithTeamsAsync();
        var finished = matches.Where(m => m.GroupName != null && m.IsFinished).ToList();

        var teamStats = new Dictionary<Guid, GroupStandingData>();

        foreach (var m in finished)
        {
            EnsureTeam(teamStats, m.HomeTeam, m.GroupName!);
            EnsureTeam(teamStats, m.AwayTeam, m.GroupName!);

            var home = teamStats[m.HomeTeam.Id];
            var away = teamStats[m.AwayTeam.Id];

            int hg = m.HomeScore!.Value, ag = m.AwayScore!.Value;

            home.Played++; away.Played++;
            home.GoalsFor += hg; home.GoalsAgainst += ag;
            away.GoalsFor += ag; away.GoalsAgainst += hg;

            if (hg > ag)       { home.Wins++;  home.Points += 3; away.Losses++; }
            else if (hg == ag) { home.Draws++; home.Points += 1; away.Draws++;  away.Points += 1; }
            else               { away.Wins++;  away.Points += 3; home.Losses++; }
        }

        var grouped = teamStats.Values
            .GroupBy(t => t.GroupName)
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(t => t.Points)
                       .ThenByDescending(t => t.GoalsFor - t.GoalsAgainst)
                       .ThenByDescending(t => t.GoalsFor)
                       .Select(t => new GroupStandingDto(
                           t.TeamId.ToString(), t.TeamName, t.IsoCode,
                           t.Points, t.Played, t.Wins, t.Draws, t.Losses,
                           t.GoalsFor, t.GoalsAgainst, t.GoalsFor - t.GoalsAgainst))
                       .ToList());

        return Ok(grouped);
    }

    [HttpPost("{id:guid}/result")]
    [Authorize]
    public async Task<IActionResult> UpdateResult(Guid id, [FromBody] UpdateMatchResultRequest request)
    {
        var match = await _matches.GetByIdAsync(id);
        if (match is null) return NotFound();

        match.HomeScore = request.HomeScore;
        match.AwayScore = request.AwayScore;
        match.IsFinished = true;
        await _matches.UpdateAsync(match);
        await _ranking.RecalculateForMatchAsync(match);

        return Ok(match);
    }

    private static void EnsureTeam(Dictionary<Guid, GroupStandingData> dict, TeamSummaryDto team, string groupName)
    {
        if (!dict.ContainsKey(team.Id))
            dict[team.Id] = new GroupStandingData(team.Id, team.Name, team.IsoCode, groupName);
    }
}

public record UpdateMatchResultRequest(int HomeScore, int AwayScore);

internal class GroupStandingData
{
    public Guid TeamId { get; }
    public string TeamName { get; }
    public string? IsoCode { get; }
    public string GroupName { get; set; }
    public int Points { get; set; }
    public int Played { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }

    public GroupStandingData(Guid teamId, string teamName, string? isoCode, string groupName)
    {
        TeamId = teamId;
        TeamName = teamName;
        IsoCode = isoCode;
        GroupName = groupName;
    }
}

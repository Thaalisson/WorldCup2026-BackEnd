using BolaoCopa.Application.DTOs;
using BolaoCopa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BolaoCopa.Api.Controllers;

[ApiController]
[Route("api/stats")]
[Authorize]
public class StatsController : ControllerBase
{
    private readonly IChampionPredictionRepository _championPredictions;
    private readonly ITeamRepository _teams;

    public StatsController(IChampionPredictionRepository championPredictions, ITeamRepository teams)
    {
        _championPredictions = championPredictions;
        _teams = teams;
    }

    [HttpGet("champion-picks")]
    public async Task<IActionResult> GetChampionPicks([FromQuery] Guid poolId)
    {
        var counts = await _championPredictions.GetChampionCountsByPoolAsync(poolId);
        if (counts.Count == 0) return Ok(Array.Empty<ChampionPickStatsDto>());

        var allTeams = await _teams.GetAllAsync();
        var teamMap = allTeams.ToDictionary(t => t.Id);

        var result = counts
            .Where(c => teamMap.ContainsKey(c.TeamId))
            .OrderByDescending(c => c.Count)
            .Select(c => new ChampionPickStatsDto(
                c.TeamId.ToString(),
                teamMap[c.TeamId].Name,
                teamMap[c.TeamId].IsoCode,
                c.Count));

        return Ok(result);
    }
}

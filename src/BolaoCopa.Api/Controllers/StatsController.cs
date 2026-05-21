using System.Security.Claims;
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
    private readonly IPoolRepository _pools;

    public StatsController(IChampionPredictionRepository championPredictions, ITeamRepository teams, IPoolRepository pools)
    {
        _championPredictions = championPredictions;
        _teams = teams;
        _pools = pools;
    }

    [HttpGet("champion-picks")]
    public async Task<IActionResult> GetChampionPicks([FromQuery] Guid poolId)
    {
        if (!await _pools.IsParticipantAsync(poolId, GetUserId())) return Forbid();

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

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

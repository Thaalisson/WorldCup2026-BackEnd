using System.Security.Claims;
using BolaoCopa.Application.DTOs;
using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BolaoCopa.Api.Controllers;

[ApiController]
[Route("api/champion-predictions")]
[Authorize]
public class ChampionPredictionsController : ControllerBase
{
    private readonly IChampionPredictionRepository _repo;
    private readonly ITeamRepository _teams;
    private readonly IPoolRepository _pools;

    public ChampionPredictionsController(IChampionPredictionRepository repo, ITeamRepository teams, IPoolRepository pools)
    {
        _repo = repo;
        _teams = teams;
        _pools = pools;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine([FromQuery] Guid poolId)
    {
        var userId = GetUserId();
        if (!await _pools.IsParticipantAsync(poolId, userId)) return Forbid();

        var prediction = await _repo.GetByUserAndPoolAsync(userId, poolId);
        if (prediction is null) return NoContent();

        var dto = await ToDto(prediction);
        return Ok(dto);
    }

    private static readonly DateTime TournamentStart = new(2026, 6, 11, 18, 0, 0, DateTimeKind.Utc);

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveChampionPredictionRequest request)
    {
        if (DateTime.UtcNow >= TournamentStart)
            return BadRequest("Palpite de campeão bloqueado — torneio já iniciou.");

        var userId = GetUserId();
        if (!await _pools.IsParticipantAsync(request.PoolId, userId)) return Forbid();

        var existing = await _repo.GetByUserAndPoolAsync(userId, request.PoolId);

        if (existing is null)
        {
            var prediction = new ChampionPrediction
            {
                Id = Guid.NewGuid(),
                PoolId = request.PoolId,
                UserId = userId,
                ChampionTeamId = request.ChampionTeamId,
                RunnerUpTeamId = request.RunnerUpTeamId,
                ThirdPlaceTeamId = request.ThirdPlaceTeamId
            };
            await _repo.AddAsync(prediction);
            return Ok(await ToDto(prediction));
        }

        existing.ChampionTeamId = request.ChampionTeamId;
        existing.RunnerUpTeamId = request.RunnerUpTeamId;
        existing.ThirdPlaceTeamId = request.ThirdPlaceTeamId;
        await _repo.UpdateAsync(existing);
        return Ok(await ToDto(existing));
    }

    private async Task<ChampionPredictionDto> ToDto(ChampionPrediction p)
    {
        var allTeams = await _teams.GetAllAsync();
        Team? Find(Guid? id) => id.HasValue ? allTeams.FirstOrDefault(t => t.Id == id.Value) : null;

        var champion = Find(p.ChampionTeamId);
        var runner = Find(p.RunnerUpTeamId);
        var third = Find(p.ThirdPlaceTeamId);

        return new ChampionPredictionDto(
            p.Id,
            p.PoolId,
            p.ChampionTeamId,
            champion?.Name ?? "",
            champion?.IsoCode,
            p.RunnerUpTeamId,
            runner?.Name,
            runner?.IsoCode,
            p.ThirdPlaceTeamId,
            third?.Name,
            third?.IsoCode,
            p.PointsEarned
        );
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

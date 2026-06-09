using System.Security.Claims;
using BolaoCopa.Application.DTOs;
using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BolaoCopa.Api.Controllers;

[ApiController]
[Route("api/group-predictions")]
[Authorize]
public class GroupPredictionsController : ControllerBase
{
    private readonly IGroupPredictionRepository _repo;
    private readonly ITeamRepository _teams;
    private readonly IPoolRepository _pools;

    public GroupPredictionsController(IGroupPredictionRepository repo, ITeamRepository teams, IPoolRepository pools)
    {
        _repo = repo;
        _teams = teams;
        _pools = pools;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyPredictions([FromQuery] Guid poolId)
    {
        var userId = GetUserId();
        if (!await _pools.IsParticipantAsync(poolId, userId)) return Forbid();

        var predictions = await _repo.GetByUserAndPoolAsync(userId, poolId);
        var allTeams = await _teams.GetAllAsync();
        var teamMap = allTeams.ToDictionary(t => t.Id);

        var result = predictions.Select(p => ToDto(p, teamMap));
        return Ok(result);
    }

    private static readonly DateTime TournamentStart = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertGroupPredictionRequest request)
    {
        if (DateTime.UtcNow >= TournamentStart)
            return BadRequest("Palpites de classificação bloqueados — torneio já iniciou.");

        var userId = GetUserId();
        if (!await _pools.IsParticipantAsync(request.PoolId, userId)) return Forbid();

        var existing = await _repo.GetByUserPoolAndGroupAsync(userId, request.PoolId, request.GroupName);

        if (existing is not null)
        {
            existing.FirstPlaceTeamId = request.FirstPlaceTeamId;
            existing.SecondPlaceTeamId = request.SecondPlaceTeamId;
            await _repo.UpdateAsync(existing);
            return Ok(new { message = "Palpite atualizado." });
        }

        await _repo.AddAsync(new GroupPrediction
        {
            Id = Guid.NewGuid(),
            PoolId = request.PoolId,
            UserId = userId,
            GroupName = request.GroupName,
            FirstPlaceTeamId = request.FirstPlaceTeamId,
            SecondPlaceTeamId = request.SecondPlaceTeamId
        });
        return Ok(new { message = "Palpite registrado." });
    }

    private static GroupPredictionDto ToDto(GroupPrediction p, Dictionary<Guid, Team> teamMap)
    {
        teamMap.TryGetValue(p.FirstPlaceTeamId, out var t1);
        teamMap.TryGetValue(p.SecondPlaceTeamId, out var t2);
        return new GroupPredictionDto(
            p.Id, p.GroupName,
            p.FirstPlaceTeamId.ToString(), t1?.Name ?? "?", t1?.IsoCode,
            p.SecondPlaceTeamId.ToString(), t2?.Name ?? "?", t2?.IsoCode,
            p.PointsEarned);
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> BulkUpsert([FromBody] BulkGroupPredictionRequest request)
    {
        if (DateTime.UtcNow >= TournamentStart)
            return BadRequest("Palpites de classificação bloqueados — torneio já iniciou.");

        if (request.Picks.Count == 0)
            return BadRequest("Nenhum palpite enviado.");

        var userId = GetUserId();
        if (!await _pools.IsParticipantAsync(request.PoolId, userId)) return Forbid();

        var existing = await _repo.GetByUserAndPoolAsync(userId, request.PoolId);
        var existingMap = existing.ToDictionary(x => x.GroupName);

        int saved = 0;
        foreach (var pick in request.Picks)
        {
            if (pick.FirstPlaceTeamId == pick.SecondPlaceTeamId) continue;

            if (existingMap.TryGetValue(pick.GroupName, out var ex))
            {
                ex.FirstPlaceTeamId = pick.FirstPlaceTeamId;
                ex.SecondPlaceTeamId = pick.SecondPlaceTeamId;
                await _repo.UpdateAsync(ex);
            }
            else
            {
                await _repo.AddAsync(new GroupPrediction
                {
                    Id = Guid.NewGuid(),
                    PoolId = request.PoolId,
                    UserId = userId,
                    GroupName = pick.GroupName,
                    FirstPlaceTeamId = pick.FirstPlaceTeamId,
                    SecondPlaceTeamId = pick.SecondPlaceTeamId
                });
            }
            saved++;
        }

        return Ok(new { saved });
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public record UpsertGroupPredictionRequest(Guid PoolId, string GroupName, Guid FirstPlaceTeamId, Guid SecondPlaceTeamId);
public record BulkGroupPredictionRequest(Guid PoolId, List<GroupPickItem> Picks);
public record GroupPickItem(string GroupName, Guid FirstPlaceTeamId, Guid SecondPlaceTeamId);

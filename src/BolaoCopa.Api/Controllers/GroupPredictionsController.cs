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

    public GroupPredictionsController(IGroupPredictionRepository repo, ITeamRepository teams)
    {
        _repo = repo;
        _teams = teams;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyPredictions([FromQuery] Guid poolId)
    {
        var userId = GetUserId();
        var predictions = await _repo.GetByUserAndPoolAsync(userId, poolId);
        var allTeams = await _teams.GetAllAsync();
        var teamMap = allTeams.ToDictionary(t => t.Id);

        var result = predictions.Select(p => ToDto(p, teamMap));
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertGroupPredictionRequest request)
    {
        var userId = GetUserId();
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

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public record UpsertGroupPredictionRequest(Guid PoolId, string GroupName, Guid FirstPlaceTeamId, Guid SecondPlaceTeamId);

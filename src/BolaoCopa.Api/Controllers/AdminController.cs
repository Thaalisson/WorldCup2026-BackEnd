using BolaoCopa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BolaoCopa.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IMatchSyncService _sync;
    private readonly IRankingService _ranking;
    private readonly IMatchRepository _matches;

    public AdminController(IMatchSyncService sync, IRankingService ranking, IMatchRepository matches)
    {
        _sync = sync;
        _ranking = ranking;
        _matches = matches;
    }

    [HttpPost("import-fixtures")]
    public async Task<IActionResult> ImportFixtures()
    {
        var count = await _sync.ImportFixturesAsync();
        return Ok(new { message = $"{count} jogo(s) importado(s).", total = count });
    }

    [HttpPost("sync-results")]
    public async Task<IActionResult> SyncResults()
    {
        await _sync.SyncResultsAsync();
        return Ok(new { message = "Resultados sincronizados." });
    }

    [HttpPost("recalculate/{poolId:guid}")]
    public async Task<IActionResult> Recalculate(Guid poolId)
    {
        await _ranking.RecalculateForPoolAsync(poolId);
        return Ok(new { message = "Ranking recalculado com sucesso." });
    }

    [HttpPost("score-group-predictions/{poolId:guid}")]
    public async Task<IActionResult> ScoreGroupPredictions(Guid poolId)
    {
        await _ranking.ScoreGroupPredictionsForPoolAsync(poolId);
        return Ok(new { message = "Palpites de classificação pontuados." });
    }

    [HttpPut("matches/{matchId:guid}/teams")]
    public async Task<IActionResult> SetMatchTeams(Guid matchId, [FromBody] SetMatchTeamsRequest request)
    {
        var match = await _matches.GetByIdAsync(matchId);
        if (match is null) return NotFound();

        if (request.HomeTeamId.HasValue)
            match.HomeTeamId = request.HomeTeamId.Value;
        if (request.AwayTeamId.HasValue)
            match.AwayTeamId = request.AwayTeamId.Value;

        await _matches.UpdateAsync(match);
        return Ok(new { message = "Times atualizados.", matchId });
    }

    [HttpPost("matches/{matchId:guid}/result")]
    public async Task<IActionResult> SetMatchResult(Guid matchId, [FromBody] SetMatchResultRequest request)
    {
        var match = await _matches.GetByIdAsync(matchId);
        if (match is null) return NotFound();

        match.HomeScore = request.HomeScore;
        match.AwayScore = request.AwayScore;
        match.IsFinished = true;
        await _matches.UpdateAsync(match);
        await _ranking.RecalculateForMatchAsync(match);

        return Ok(new { message = "Resultado registrado e ranking atualizado." });
    }
}

public record SetMatchTeamsRequest(Guid? HomeTeamId, Guid? AwayTeamId);
public record SetMatchResultRequest(int HomeScore, int AwayScore);

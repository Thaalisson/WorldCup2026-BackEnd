using System.Text;
using BolaoCopa.Application.DTOs;
using BolaoCopa.Application.Interfaces;
using BolaoCopa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa.Api;

/// <summary>
/// Valida a pontuação: recalcula os pontos de cada palpite (via ScoringService) e
/// compara com o que está salvo no banco; soma por usuário e confere com o total do
/// ranking. Também sinaliza palpites que pontuaram SEM acertar o vencedor (rótulo
/// "SÓ O VENCEDOR" enganoso). Apenas leitura — não altera nada.
/// </summary>
public class ScoringValidator
{
    private readonly AppDbContext _db;
    private readonly IScoringService _scoring;

    public ScoringValidator(AppDbContext db, IScoringService scoring)
    {
        _db = db;
        _scoring = scoring;
    }

    public async Task<string> RunAsync(string? poolFilter)
    {
        var sb = new StringBuilder();
        var teams = await _db.Teams.ToDictionaryAsync(t => t.Id);
        var finished = await _db.Matches.Where(m => m.IsFinished).ToListAsync();
        var finishedIds = finished.Select(m => m.Id).ToHashSet();

        string Label(Domain.Entities.Match m) =>
            $"{teams[m.HomeTeamId].Code} {m.HomeScore}×{m.AwayScore} {teams[m.AwayTeamId].Code}";

        sb.AppendLine($"Jogos finalizados: {finished.Count} ({string.Join(", ", finished.Select(Label))})");

        var pools = await _db.Pools
            .Where(p => poolFilter == null || p.Name.Contains(poolFilter) || p.InviteCode.Contains(poolFilter))
            .OrderBy(p => p.Name).ToListAsync();

        int mismatchPts = 0, mismatchTotal = 0, mislabeled = 0;

        foreach (var pool in pools)
        {
            var cfg = new ScoringConfigDto(pool.PointsExactScore, pool.PointsCorrectResult,
                pool.PointsChampion, pool.PointsRunnerUp, pool.PointsThirdPlace, pool.PointsGroupQualifier);

            var parts = await (from pp in _db.PoolParticipants
                               join u in _db.Users on pp.UserId equals u.Id
                               where pp.PoolId == pool.Id
                               orderby pp.TotalPoints descending
                               select new { pp.UserId, u.Name, pp.TotalPoints }).ToListAsync();

            sb.AppendLine($"\n### {pool.Name}  (exato={cfg.PointsExactScore}, vencedor={cfg.PointsCorrectResult})");

            foreach (var p in parts)
            {
                var preds = await _db.Predictions
                    .Where(x => x.PoolId == pool.Id && x.UserId == p.UserId && finishedIds.Contains(x.MatchId))
                    .ToListAsync();

                int somaSalva = 0, somaRecalc = 0;
                var lines = new List<string>();
                foreach (var pred in preds.OrderBy(x => x.MatchId))
                {
                    var m = finished.First(f => f.Id == pred.MatchId);
                    var recalc = _scoring.CalculateMatchPredictionPoints(pred, m, cfg);
                    somaSalva += pred.PointsEarned;
                    somaRecalc += recalc;

                    var venceuReal = m.HomeScore!.Value.CompareTo(m.AwayScore!.Value);
                    var venceuPalp = pred.HomeScorePrediction.CompareTo(pred.AwayScorePrediction);
                    var acertouVencedor = venceuReal == venceuPalp;
                    var exato = pred.HomeScorePrediction == m.HomeScore && pred.AwayScorePrediction == m.AwayScore;

                    var flagStore = recalc != pred.PointsEarned ? $"  ❗SALVO={pred.PointsEarned} RECALC={recalc}" : "";
                    if (recalc != pred.PointsEarned) mismatchPts++;
                    var flagLabel = "";
                    if (!exato && pred.PointsEarned > 0 && !acertouVencedor)
                    {
                        flagLabel = "  ⚠ pontuou SEM acertar vencedor (rótulo 'SÓ O VENCEDOR' errado)";
                        mislabeled++;
                    }

                    var tag = exato ? "PLACAR EXATO" : acertouVencedor ? "vencedor" : "parcial";
                    lines.Add($"    {Label(m)} | palpitou {pred.HomeScorePrediction}×{pred.AwayScorePrediction} | {pred.PointsEarned} pts ({tag}){flagStore}{flagLabel}");
                }

                var bateTotal = somaSalva == p.TotalPoints;
                if (!bateTotal) mismatchTotal++;
                sb.AppendLine($"  {p.Name} — ranking: {p.TotalPoints} | soma jogos: {somaSalva} {(bateTotal ? "✓" : "❗ NÃO BATE")}");
                foreach (var l in lines) sb.AppendLine(l);
            }
        }

        sb.AppendLine($"\n=== RESUMO ===");
        sb.AppendLine($"Pontos por palpite divergentes (salvo≠recalc): {mismatchPts}");
        sb.AppendLine($"Totais de ranking divergentes (soma≠ranking): {mismatchTotal}");
        sb.AppendLine($"Palpites rotulados 'SÓ O VENCEDOR' sem acertar o vencedor: {mislabeled}");
        return sb.ToString();
    }
}

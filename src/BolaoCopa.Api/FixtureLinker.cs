using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using BolaoCopa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa.Api;

/// <summary>
/// Vincula os jogos/seleções que JÁ existem no banco (vindos do seed) aos IDs reais
/// do football-data.org, preenchendo ApiFootballTeamId e ApiFootballFixtureId — sem
/// apagar nada. Assim o sync automático passa a funcionar preservando todos os palpites.
///
/// Estratégia:
///  - Seleções: casadas por código de 3 letras (Code == tla), com fallback por eliminação
///    dentro de cada grupo (cobre divergências como URU vs URY).
///  - Jogos de grupo: casados pelo PAR de seleções (cada par se enfrenta 1x no grupo),
///    o que dispensa comparar data/fuso.
///  - Mata-mata: seleções ainda indefinidas (TBD); casados por data+hora de início.
/// </summary>
public class FixtureLinker
{
    private readonly AppDbContext _db;
    private readonly IFootballApiClient _api;

    public FixtureLinker(AppDbContext db, IFootballApiClient api)
    {
        _db = db;
        _api = api;
    }

    public async Task<string> RunAsync(bool apply)
    {
        var real = await _api.GetAllFixturesAsync();
        var realGroup = real.Where(f => f.Round.StartsWith("Group", StringComparison.OrdinalIgnoreCase)).ToList();
        var realKnockout = real.Where(f => !f.Round.StartsWith("Group", StringComparison.OrdinalIgnoreCase)).ToList();

        var teams = await _db.Teams.ToListAsync();
        var matches = await _db.Matches.ToListAsync();
        var log = new List<string>();

        // 1) Mapa seleção local -> id real, por grupo
        var teamMap = new Dictionary<Guid, int>();
        foreach (var grp in teams.Where(t => t.GroupName != null).GroupBy(t => t.GroupName))
        {
            var realInGroup = realGroup
                .Where(f => f.Round.Equals($"Group {grp.Key}", StringComparison.OrdinalIgnoreCase))
                .SelectMany(f => new[]
                {
                    (Id: f.HomeTeamId, Code: f.HomeTeamCode),
                    (Id: f.AwayTeamId, Code: f.AwayTeamCode)
                })
                .DistinctBy(x => x.Id)
                .ToList();

            var used = new HashSet<int>();
            var pendingLocal = new List<Team>();

            foreach (var lt in grp)
            {
                var hit = realInGroup.FirstOrDefault(r =>
                    !used.Contains(r.Id) &&
                    r.Code.Equals(lt.Code, StringComparison.OrdinalIgnoreCase));
                if (hit.Id != 0) { teamMap[lt.Id] = hit.Id; used.Add(hit.Id); }
                else pendingLocal.Add(lt);
            }

            // Fallback por eliminação (códigos que não batem exatamente)
            var leftover = realInGroup.Where(r => !used.Contains(r.Id)).ToList();
            if (pendingLocal.Count == leftover.Count)
            {
                for (var i = 0; i < pendingLocal.Count; i++)
                {
                    teamMap[pendingLocal[i].Id] = leftover[i].Id;
                    log.Add($"  ~ grupo {grp.Key}: '{pendingLocal[i].Code}' -> id {leftover[i].Id} (por eliminação)");
                }
            }
            else if (pendingLocal.Count > 0)
            {
                log.Add($"  ! grupo {grp.Key}: {pendingLocal.Count} seleção(ões) sem casamento claro: " +
                        string.Join(", ", pendingLocal.Select(t => t.Code)));
            }
        }

        var teamsLinked = 0;
        foreach (var t in teams)
            if (teamMap.TryGetValue(t.Id, out var rid) && t.ApiFootballTeamId != rid)
            {
                t.ApiFootballTeamId = rid;
                teamsLinked++;
            }

        // 2) Jogos de grupo: casar pelo par de seleções (independe de data/fuso)
        var groupLinked = 0;
        var groupUnlinked = new List<Match>();
        foreach (var m in matches.Where(m => m.GroupName != null))
        {
            if (!teamMap.TryGetValue(m.HomeTeamId, out var rh) ||
                !teamMap.TryGetValue(m.AwayTeamId, out var ra))
            {
                groupUnlinked.Add(m);
                continue;
            }

            var rf = realGroup.FirstOrDefault(f =>
                (f.HomeTeamId == rh && f.AwayTeamId == ra) ||
                (f.HomeTeamId == ra && f.AwayTeamId == rh));

            if (rf is not null) { m.ApiFootballFixtureId = rf.FixtureId; groupLinked++; }
            else groupUnlinked.Add(m);
        }

        // 3) Mata-mata: seleções TBD -> casar por data/hora de início
        var koLinked = 0;
        var koUnlinked = 0;
        foreach (var m in matches.Where(m => m.GroupName == null))
        {
            var sameTime = realKnockout.Where(f => f.Date == m.KickoffAt).ToList();
            if (sameTime.Count == 1) { m.ApiFootballFixtureId = sameTime[0].FixtureId; koLinked++; }
            else koUnlinked++;
        }

        if (apply) await _db.SaveChangesAsync();

        var header = apply ? "APLICADO" : "PRÉVIA (nada gravado)";
        return $"[link-fixtures] {header}\n" +
               $"  Seleções vinculadas: {teamsLinked}/{teams.Count}\n" +
               $"  Jogos de grupo vinculados: {groupLinked}/72 (não vinculados: {groupUnlinked.Count})\n" +
               $"  Jogos de mata-mata vinculados por data: {koLinked} (pendentes: {koUnlinked})\n" +
               (log.Count > 0 ? "  Observações:\n" + string.Join("\n", log) : "  Sem divergências de código.");
    }
}

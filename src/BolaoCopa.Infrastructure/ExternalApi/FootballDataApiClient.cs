using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using BolaoCopa.Application.DTOs;
using BolaoCopa.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BolaoCopa.Infrastructure.ExternalApi;

/// <summary>
/// Cliente do football-data.org (v4). Substitui a api-football: o plano gratuito já
/// dá acesso aos jogos reais da Copa do Mundo (competição "WC").
/// Autenticação via header X-Auth-Token; base address e token vêm da configuração.
/// </summary>
public class FootballDataApiClient : IFootballApiClient
{
    private readonly HttpClient _http;
    private readonly string _competition;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public FootballDataApiClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _competition = config["FootballData:Competition"] ?? "WC";
    }

    public async Task<IReadOnlyList<ApiFixtureDto>> GetAllFixturesAsync()
    {
        var raw = await _http.GetFromJsonAsync<FdMatchesResponse>(
            $"competitions/{_competition}/matches", JsonOpts);
        return Map(raw?.Matches ?? []);
    }

    public async Task<IReadOnlyList<ApiFixtureDto>> GetTodayFinishedFixturesAsync()
    {
        // Retorna TODOS os jogos já finalizados (não só os de hoje): o MatchSyncService
        // ignora os que já estão marcados como finished, então isso é idempotente e
        // se auto-recupera caso o job fique fora do ar por algum período.
        var raw = await _http.GetFromJsonAsync<FdMatchesResponse>(
            $"competitions/{_competition}/matches?status=FINISHED", JsonOpts);
        return Map((raw?.Matches ?? [])
            .Where(m => m.Score.FullTime.Home is not null && m.Score.FullTime.Away is not null)
            .ToList());
    }

    private static IReadOnlyList<ApiFixtureDto> Map(IEnumerable<FdMatch> matches) =>
        matches.Select(m => new ApiFixtureDto(
            m.Id,
            ParseUtc(m.UtcDate),
            m.Status,
            BuildRound(m),
            m.HomeTeam.Id ?? 0,
            m.HomeTeam.Name ?? "",
            m.AwayTeam.Id ?? 0,
            m.AwayTeam.Name ?? "",
            m.Score.FullTime.Home,
            m.Score.FullTime.Away,
            m.HomeTeam.Tla ?? "",
            m.AwayTeam.Tla ?? ""
        )).ToList();

    private static DateTime ParseUtc(string iso) =>
        DateTime.Parse(iso, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    // Converte stage/group do football-data num texto de "round" que o MatchSyncService
    // (MapStage / ExtractGroup) já sabe interpretar — assim a sincronização não muda.
    private static string BuildRound(FdMatch m)
    {
        if (m.Stage == "GROUP_STAGE")
        {
            var letter = (m.Group ?? "").Replace("GROUP_", "").Trim();
            return $"Group {letter}".Trim();
        }

        return m.Stage switch
        {
            "LAST_32"        => "Round of 32",
            "LAST_16"        => "Round of 16",
            "QUARTER_FINALS" => "Quarter-finals",
            "SEMI_FINALS"    => "Semi-finals",
            "THIRD_PLACE"    => "3rd Place Final",
            "FINAL"          => "Final",
            _                => m.Stage
        };
    }
}

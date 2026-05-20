using System.Net.Http.Json;
using System.Text.Json;
using BolaoCopa.Application.DTOs;
using BolaoCopa.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BolaoCopa.Infrastructure.ExternalApi;

public class FootballApiClient : IFootballApiClient
{
    private readonly HttpClient _http;
    private readonly int _leagueId;
    private readonly int _season;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    private static readonly string[] FinishedStatuses = ["FT", "AET", "PEN"];

    public FootballApiClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _leagueId = int.Parse(config["ApiFootball:LeagueId"] ?? "1");
        _season = int.Parse(config["ApiFootball:Season"] ?? "2026");
    }

    public async Task<IReadOnlyList<ApiFixtureDto>> GetAllFixturesAsync()
    {
        var raw = await _http.GetFromJsonAsync<ApiResponse<ApiFixture>>(
            $"/fixtures?league={_leagueId}&season={_season}", JsonOpts);
        return Map(raw?.Response ?? []);
    }

    public async Task<IReadOnlyList<ApiFixtureDto>> GetTodayFinishedFixturesAsync()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var raw = await _http.GetFromJsonAsync<ApiResponse<ApiFixture>>(
            $"/fixtures?league={_leagueId}&season={_season}&date={date}", JsonOpts);
        return Map((raw?.Response ?? [])
            .Where(f => FinishedStatuses.Contains(f.Fixture.Status.Short))
            .ToList());
    }

    private static IReadOnlyList<ApiFixtureDto> Map(IEnumerable<ApiFixture> fixtures) =>
        fixtures.Select(f => new ApiFixtureDto(
            f.Fixture.Id,
            f.Fixture.Date,
            f.Fixture.Status.Short,
            f.League.Round,
            f.Teams.Home.Id,
            f.Teams.Home.Name,
            f.Teams.Away.Id,
            f.Teams.Away.Name,
            f.Goals.Home,
            f.Goals.Away
        )).ToList();
}

namespace BolaoCopa.Infrastructure.ExternalApi;

// Modelos que espelham o JSON bruto da api-football.com v3
// Usados apenas internamente em FootballApiClient

internal class ApiResponse<T>
{
    public List<T> Response { get; set; } = [];
}

internal class ApiFixture
{
    public ApiFixtureInfo Fixture { get; set; } = new();
    public ApiLeagueInfo League { get; set; } = new();
    public ApiTeamPair Teams { get; set; } = new();
    public ApiGoals Goals { get; set; } = new();
}

internal class ApiFixtureInfo
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public ApiFixtureStatus Status { get; set; } = new();
}

internal class ApiFixtureStatus
{
    public string Short { get; set; } = "";
}

internal class ApiLeagueInfo
{
    public string Round { get; set; } = "";
}

internal class ApiTeamPair
{
    public ApiTeamInfo Home { get; set; } = new();
    public ApiTeamInfo Away { get; set; } = new();
}

internal class ApiTeamInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

internal class ApiGoals
{
    public int? Home { get; set; }
    public int? Away { get; set; }
}

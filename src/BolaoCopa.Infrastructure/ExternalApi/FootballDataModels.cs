namespace BolaoCopa.Infrastructure.ExternalApi;

// Modelos que espelham o JSON bruto do football-data.org v4
// (GET /v4/competitions/{code}/matches). Usados apenas internamente em FootballDataApiClient.

internal class FdMatchesResponse
{
    public List<FdMatch> Matches { get; set; } = [];
}

internal class FdMatch
{
    public int Id { get; set; }
    // ISO 8601 com sufixo "Z" — parseado para UTC manualmente em FootballDataApiClient.
    public string UtcDate { get; set; } = "";
    public string Status { get; set; } = "";
    public string Stage { get; set; } = "";
    public string? Group { get; set; }
    public FdTeam HomeTeam { get; set; } = new();
    public FdTeam AwayTeam { get; set; } = new();
    public FdScore Score { get; set; } = new();
}

internal class FdTeam
{
    // Anuláveis: nos jogos de mata-mata as seleções ainda são indefinidas (TBD/null).
    public int? Id { get; set; }
    public string? Name { get; set; }
    // Sigla de 3 letras (ex.: "MEX", "RSA"). Usada como Code da seleção.
    public string? Tla { get; set; }
}

internal class FdScore
{
    public FdScoreLine FullTime { get; set; } = new();
}

internal class FdScoreLine
{
    public int? Home { get; set; }
    public int? Away { get; set; }
}

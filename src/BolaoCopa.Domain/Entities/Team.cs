namespace BolaoCopa.Domain.Entities;

public class Team
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? IsoCode { get; set; }
    public string? GroupName { get; set; }
    public int? ApiFootballTeamId { get; set; }
}

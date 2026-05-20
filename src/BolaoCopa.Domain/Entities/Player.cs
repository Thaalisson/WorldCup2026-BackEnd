namespace BolaoCopa.Domain.Entities;

public class Player
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string? Club { get; set; }
    public int? Age { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
}

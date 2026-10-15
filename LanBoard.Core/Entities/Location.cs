namespace LanBoard.Core.Entities;

public class Location
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Directions { get; set; }
    public string? Parking { get; set; }
    public string? Notes { get; set; }

    public ICollection<LanParty> Parties { get; set; } = [];
}

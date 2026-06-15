namespace LanBoard.Core.Entities;

public class Seat
{
    public Guid Id { get; set; }
    public Guid PartyId { get; set; }
    public required string Label { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public Guid? AssignedUserId { get; set; }

    public LanParty Party { get; set; } = null!;
    public User? AssignedUser { get; set; }
}

namespace LanBoard.Core.Entities;

public class LanParty
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTime Date { get; set; }
    public Guid LocationId { get; set; }
    public string? InviteCode { get; set; }
    public bool IsActive { get; set; }
    public string? InfoBoard { get; set; }
    public Guid CreatedByUserId { get; set; }

    public Location Location { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<Seat> Seats { get; set; } = [];
    public ICollection<Session> Sessions { get; set; } = [];
}

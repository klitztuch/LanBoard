namespace LanBoard.Core.Entities;

public class User
{
    public Guid Id { get; set; }
    public required string DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<UserIdentity> Identities { get; set; } = [];
    public ICollection<LanParty> CreatedParties { get; set; } = [];
    public ICollection<Seat> AssignedSeats { get; set; } = [];
    public ICollection<Session> Sessions { get; set; } = [];
}

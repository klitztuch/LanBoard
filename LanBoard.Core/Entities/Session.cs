namespace LanBoard.Core.Entities;

public class Session
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PartyId { get; set; }
    public string? GameAppId { get; set; }
    public string? GameName { get; set; }
    public DateTime LastSeen { get; set; }
    public DateTime JoinedAt { get; set; }

    public User User { get; set; } = null!;
    public LanParty Party { get; set; } = null!;
}

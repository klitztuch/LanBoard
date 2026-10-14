namespace LanBoard.Core.Entities;

public class Rsvp
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PartyId { get; set; }
    public bool IsAttending { get; set; }
    public DateTime RespondedAt { get; set; }

    public User User { get; set; } = null!;
    public LanParty Party { get; set; } = null!;
}

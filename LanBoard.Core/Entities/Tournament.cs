namespace LanBoard.Core.Entities;

public class Tournament
{
    public Guid Id { get; set; }
    public Guid PartyId { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsStarted { get; set; }

    public LanParty Party { get; set; } = null!;
    public ICollection<TournamentParticipant> Participants { get; set; } = [];
    public ICollection<TournamentMatch> Matches { get; set; } = [];
}

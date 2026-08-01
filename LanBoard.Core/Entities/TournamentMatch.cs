namespace LanBoard.Core.Entities;

public class TournamentMatch
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public int Round { get; set; }
    public int Slot { get; set; }
    public Guid? Participant1Id { get; set; }
    public Guid? Participant2Id { get; set; }
    public Guid? WinnerId { get; set; }

    public Tournament Tournament { get; set; } = null!;
    public TournamentParticipant? Participant1 { get; set; }
    public TournamentParticipant? Participant2 { get; set; }
    public TournamentParticipant? Winner { get; set; }
}

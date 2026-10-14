namespace LanBoard.Core.Entities;

public class TournamentParticipant
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public Guid UserId { get; set; }

    public Tournament Tournament { get; set; } = null!;
    public User User { get; set; } = null!;
}

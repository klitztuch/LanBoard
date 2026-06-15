namespace LanBoard.Core.Entities;

public class UserIdentity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Provider { get; set; }
    public required string ProviderUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}

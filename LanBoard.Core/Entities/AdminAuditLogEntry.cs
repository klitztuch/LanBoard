namespace LanBoard.Core.Entities;

public class AdminAuditLogEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Action { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}

namespace LanBoard.Core.Entities;

public class Role
{
    public static readonly Guid AdminId = new("00000000-0000-0000-0000-00000000a0d1");

    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public List<string> Permissions { get; set; } = [];
    public bool IsSystem { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();
}

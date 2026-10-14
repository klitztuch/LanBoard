namespace LanBoard.Core.Entities;

public class User
{
    public Guid Id { get; set; }
    public required string DisplayName { get; set; }  // Effective_Name = Nickname ?? SteamName; maintained via SetNickname/SyncSteamName
    public required string SteamName { get; set; }    // provider name; dev login: initial name
    public string? Nickname { get; private set; }
    public string? AvatarUrl { get; set; }
    public string? PreferredCulture { get; set; }   // null = no explicit choice
    public DateTime CreatedAt { get; set; }

    public ICollection<UserIdentity> Identities { get; set; } = [];
    public ICollection<LanParty> CreatedParties { get; set; } = [];
    public ICollection<Seat> AssignedSeats { get; set; } = [];
    public ICollection<Session> Sessions { get; set; } = [];
    public ICollection<UserRole> UserRoles { get; set; } = [];

    // Null or whitespace clears the nickname; validation (length, uniqueness) is the caller's job.
    public void SetNickname(string? nickname)
    {
        Nickname = string.IsNullOrWhiteSpace(nickname) ? null : nickname.Trim();
        DisplayName = Nickname ?? SteamName;
    }

    // Never touches the nickname, so a Steam login can't overwrite a user-chosen name.
    public void SyncSteamName(string steamName)
    {
        SteamName = steamName;
        DisplayName = Nickname ?? SteamName;
    }
}

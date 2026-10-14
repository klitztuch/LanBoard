namespace LanBoard.Core.Entities;

// Created lazily on first settings save; a missing row means defaults.
public class UserProfile
{
    public Guid UserId { get; set; }
    public string? RealName { get; set; }
    public FieldVisibility RealNameVisibility { get; set; } = FieldVisibility.Orga;
    public string? Clan { get; set; }
    public List<string> FavoriteGames { get; set; } = [];
    public string? ContactEmail { get; set; }
    public List<string> NotificationOptIns { get; set; } = [];
    // In-app notifications default to on, so only deviations are stored.
    public List<string> InAppOptOuts { get; set; } = [];
    public bool NotificationSoundEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<ProfileFieldValue> FieldValues { get; set; } = [];
}

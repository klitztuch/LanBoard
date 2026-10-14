using LanBoard.Core.Entities;

namespace LanBoard.Application.Profiles;

public interface IProfileService
{
    /// <summary>Profile of <paramref name="userId"/> as seen by <paramref name="viewerUserId"/>; values filtered by visibility. Never contains the e-mail.</summary>
    Task<ProfileView> GetProfileAsync(Guid userId, Guid viewerUserId, CancellationToken ct = default);

    /// <summary>The caller's own editable data, including e-mail and notification opt-ins.</summary>
    Task<ProfileSettings> GetSettingsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Validates everything first and throws a ValidationException with all field errors; saves atomically.</summary>
    Task SaveSettingsAsync(Guid userId, ProfileSettingsInput input, CancellationToken ct = default);

    /// <summary>Attending users of a party with their Public and Orga custom values. Requires profile.read.</summary>
    Task<PartyProfileOverview> GetPartyOverviewAsync(Guid partyId, Guid performedByUserId, CancellationToken ct = default);
}

public sealed record ProfileView(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    string? SteamProfileUrl,
    string? RealName,                                   // null if unset or not visible
    string? Clan,
    IReadOnlyList<string> FavoriteGames,
    IReadOnlyList<CustomFieldValueView> CustomFields,   // only visible, non-empty, by SortOrder
    IReadOnlyList<PastPartyView> PastParties,           // newest first
    IReadOnlyList<PlayedGameView> PlayedGames,          // by total playtime desc
    bool IsOwnProfile);

public sealed record CustomFieldValueView(Guid FieldId, string Key, IReadOnlyDictionary<string, string> Labels, ProfileFieldType Type, string Value);
public sealed record PastPartyView(Guid PartyId, string Name, DateTime Date, string Location);
public sealed record PlayedGameView(string GameName, TimeSpan TotalPlaytime, int PartyCount);

public sealed record ProfileSettings(
    string SteamName,
    string? Nickname,
    string? RealName,
    FieldVisibility RealNameVisibility,
    string? Clan,
    IReadOnlyList<string> FavoriteGames,
    string? ContactEmail,
    IReadOnlySet<string> NotificationOptIns,
    IReadOnlyList<ProfileFieldDefinition> Fields,       // all definitions, by SortOrder
    IReadOnlyDictionary<Guid, string> FieldValues);

/// <summary>Custom fields missing from <see cref="FieldValues"/> keep their stored value; an empty value deletes it.</summary>
public sealed record ProfileSettingsInput(
    string? Nickname,
    string? RealName,
    FieldVisibility RealNameVisibility,
    string? Clan,
    IReadOnlyList<string> FavoriteGames,
    string? ContactEmail,
    IReadOnlySet<string> NotificationOptIns,
    IReadOnlyDictionary<Guid, string?> FieldValues)
{
    /// <summary>ValidationError.Field for a custom field value.</summary>
    public static string CustomFieldName(Guid fieldId) => $"custom:{fieldId}";
}

public sealed record PartyProfileOverview(
    IReadOnlyList<ProfileFieldDefinition> Columns,      // Public + Orga definitions
    IReadOnlyList<PartyProfileRow> Rows);               // attending users, by DisplayName

public sealed record PartyProfileRow(Guid UserId, string DisplayName, IReadOnlyDictionary<Guid, string> Values);

using System.Net.Mail;
using LanBoard.Application.Authorization;
using LanBoard.Application.Errors;
using LanBoard.Application.Interfaces;
using LanBoard.Application.Notifications;
using LanBoard.Application.Profiles;
using LanBoard.Core.Entities;

namespace LanBoard.Infrastructure.Services;

public class ProfileService(
    IUserRepository users,
    IUserProfileRepository profiles,
    IProfileFieldRepository fields,
    IRsvpRepository rsvps,
    ISessionRepository sessions,
    ILanPartyRepository parties,
    IPermissionService permissionService) : IProfileService
{
    private const int MaxNicknameLength = 32;
    private const int MaxRealNameLength = 100;
    private const int MaxClanLength = 50;
    private const int MaxFavoriteGames = 10;
    private const int MaxFavoriteGameLength = 50;
    private const int MaxEmailLength = 254;
    private const int MaxTextValueLength = 200;
    private const string Yes = "yes";
    private const string No = "no";

    public async Task<ProfileView> GetProfileAsync(Guid userId, Guid viewerUserId, CancellationToken ct = default)
    {
        var user = await users.GetWithIdentitiesAsync(userId, ct)
            ?? throw new BusinessRuleException(ErrorCodes.UserNotFound);
        var profile = await profiles.GetAsync(userId, ct) ?? new UserProfile { UserId = userId };
        var definitions = await fields.GetAllAsync(ct);
        var viewer = new Viewer(viewerUserId, await permissionService.GetEffectivePermissionsAsync(viewerUserId, ct));

        var values = profile.FieldValues.ToDictionary(v => v.FieldId, v => v.Value);
        var customFields = definitions
            .Where(d => values.ContainsKey(d.Id) && ProfileVisibility.CanSee(d.Visibility, userId, viewer))
            .Select(d => new CustomFieldValueView(d.Id, d.Key, d.Labels, d.Type, values[d.Id]))
            .ToList();

        var pastParties = (await rsvps.GetAttendedPastPartiesAsync(userId, DateTime.UtcNow.Date, ct))
            .Select(p => new PastPartyView(p.Id, p.Name, p.Date, p.Location))
            .ToList();

        var playedGames = (await sessions.GetByUserAsync(userId, ct))
            .Where(s => s.GameName is not null)
            .GroupBy(s => s.GameName!)
            .Select(g => new PlayedGameView(g.Key, SessionPlaytime.Sum(g), g.Select(s => s.PartyId).Distinct().Count()))
            .OrderByDescending(g => g.TotalPlaytime)
            .ToList();

        var steamId = user.Identities.FirstOrDefault(i => i.Provider == "Steam")?.ProviderUserId;

        return new ProfileView(
            user.Id,
            user.DisplayName,
            user.AvatarUrl,
            steamId is null ? null : $"https://steamcommunity.com/profiles/{steamId}",
            ProfileVisibility.CanSee(profile.RealNameVisibility, userId, viewer) ? profile.RealName : null,
            profile.Clan,
            profile.FavoriteGames,
            customFields,
            pastParties,
            playedGames,
            userId == viewerUserId);
    }

    public async Task<ProfileSettings> GetSettingsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.GetWithIdentitiesAsync(userId, ct)
            ?? throw new BusinessRuleException(ErrorCodes.UserNotFound);
        var profile = await profiles.GetAsync(userId, ct) ?? new UserProfile { UserId = userId };
        var definitions = await fields.GetAllAsync(ct);

        return new ProfileSettings(
            user.SteamName,
            user.Nickname,
            profile.RealName,
            profile.RealNameVisibility,
            profile.Clan,
            profile.FavoriteGames,
            profile.ContactEmail,
            profile.NotificationOptIns.ToHashSet(),
            definitions,
            profile.FieldValues.ToDictionary(v => v.FieldId, v => v.Value));
    }

    public async Task SaveSettingsAsync(Guid userId, ProfileSettingsInput input, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct)
            ?? throw new BusinessRuleException(ErrorCodes.UserNotFound);
        var profile = await profiles.GetForUpdateAsync(userId, ct);
        var definitions = (await fields.GetAllAsync(ct)).ToDictionary(d => d.Id);

        var errors = new List<ValidationError>();

        var nickname = Normalize(input.Nickname);
        if (nickname is { Length: > MaxNicknameLength })
            errors.Add(new(nameof(input.Nickname), ErrorCodes.ProfileNicknameInvalid, MaxNicknameLength));
        // Only checked on change, so an unchanged nickname stays saveable if someone else later takes the name.
        else if (nickname is not null && nickname != user.Nickname && await users.DisplayNameTakenAsync(nickname, userId, ct))
            errors.Add(new(nameof(input.Nickname), ErrorCodes.ProfileNicknameTaken));

        var realName = Normalize(input.RealName);
        if (realName is { Length: > MaxRealNameLength })
            errors.Add(new(nameof(input.RealName), ErrorCodes.ProfileValueTooLong, MaxRealNameLength));

        var clan = Normalize(input.Clan);
        if (clan is { Length: > MaxClanLength })
            errors.Add(new(nameof(input.Clan), ErrorCodes.ProfileValueTooLong, MaxClanLength));

        var favoriteGames = input.FavoriteGames
            .Select(g => g.Trim())
            .Where(g => g.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (favoriteGames.Count > MaxFavoriteGames)
            errors.Add(new(nameof(input.FavoriteGames), ErrorCodes.ProfileTooManyFavoriteGames, MaxFavoriteGames));
        else if (favoriteGames.Any(g => g.Length > MaxFavoriteGameLength))
            errors.Add(new(nameof(input.FavoriteGames), ErrorCodes.ProfileValueTooLong, MaxFavoriteGameLength));

        var email = Normalize(input.ContactEmail);
        if (email is not null && !IsValidEmail(email))
            errors.Add(new(nameof(input.ContactEmail), ErrorCodes.ProfileEmailInvalid));

        if (input.NotificationOptIns.FirstOrDefault(c => !NotificationCategories.All.Contains(c)) is { } unknownCategory)
            errors.Add(new(nameof(input.NotificationOptIns), ErrorCodes.ProfileUnknownCategory, unknownCategory));

        // Resulting custom values: stored values, overlaid with the submitted ones (null = delete).
        var resultingValues = (profile?.FieldValues ?? [])
            .ToDictionary(v => v.FieldId, v => (string?)v.Value);
        foreach (var (fieldId, raw) in input.FieldValues)
        {
            var value = Normalize(raw);
            resultingValues[fieldId] = value;
            if (!definitions.TryGetValue(fieldId, out var definition))
                errors.Add(new(ProfileSettingsInput.CustomFieldName(fieldId), ErrorCodes.ProfileFieldNotFound));
            else if (value is not null && ValidateCustomValue(definition, value) is { } error)
                errors.Add(error);
        }

        errors.AddRange(definitions.Values
            .Where(d => d.IsRequired && resultingValues.GetValueOrDefault(d.Id) is null)
            .Select(d => new ValidationError(ProfileSettingsInput.CustomFieldName(d.Id), ErrorCodes.ProfileFieldRequired)));

        if (errors.Count > 0)
            throw new ValidationException(errors);

        user.SetNickname(nickname);

        if (profile is null)
        {
            profile = new UserProfile { UserId = userId };
            await profiles.AddAsync(profile, ct);
        }

        profile.RealName = realName;
        profile.RealNameVisibility = input.RealNameVisibility;
        profile.Clan = clan;
        profile.FavoriteGames = favoriteGames;
        profile.ContactEmail = email;
        profile.NotificationOptIns = input.NotificationOptIns.Distinct().Order().ToList();
        profile.UpdatedAt = DateTime.UtcNow;
        ApplyCustomValues(profile, input.FieldValues);

        await profiles.SaveChangesAsync(ct);
    }

    public async Task<PartyProfileOverview> GetPartyOverviewAsync(Guid partyId, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissionService.EnsureAsync(performedByUserId, Permissions.ProfileRead, ct);

        _ = await parties.GetByIdAsync(partyId, ct)
            ?? throw new BusinessRuleException(ErrorCodes.PartyNotFound);

        var columns = (await fields.GetAllAsync(ct))
            .Where(d => d.Visibility != FieldVisibility.Private)
            .ToList();
        var columnIds = columns.Select(c => c.Id).ToHashSet();

        var attendees = await rsvps.GetAttendingUsersAsync(partyId, ct);
        var valuesByUser = (await profiles.GetByUserIdsAsync(attendees.Select(u => u.Id), ct))
            .ToDictionary(
                p => p.UserId,
                p => (IReadOnlyDictionary<Guid, string>)p.FieldValues
                    .Where(v => columnIds.Contains(v.FieldId))
                    .ToDictionary(v => v.FieldId, v => v.Value));

        var rows = attendees
            .Select(u => new PartyProfileRow(u.Id, u.DisplayName, valuesByUser.GetValueOrDefault(u.Id) ?? new Dictionary<Guid, string>()))
            .ToList();

        return new PartyProfileOverview(columns, rows);
    }

    private static ValidationError? ValidateCustomValue(ProfileFieldDefinition definition, string value)
    {
        var field = ProfileSettingsInput.CustomFieldName(definition.Id);
        return definition.Type switch
        {
            ProfileFieldType.Text when value.Length > MaxTextValueLength
                => new(field, ErrorCodes.ProfileValueTooLong, MaxTextValueLength),
            ProfileFieldType.SingleChoice when !definition.Choices.Contains(value, StringComparer.Ordinal)
                => new(field, ErrorCodes.ProfileFieldInvalidChoice),
            ProfileFieldType.YesNo when value is not (Yes or No)
                => new(field, ErrorCodes.ProfileFieldInvalidChoice),
            _ => null,
        };
    }

    // Empty values are never stored: clearing a value deletes its row (orphan removal on the required FK).
    private static void ApplyCustomValues(UserProfile profile, IReadOnlyDictionary<Guid, string?> submitted)
    {
        foreach (var (fieldId, raw) in submitted)
        {
            var value = Normalize(raw);
            var existing = profile.FieldValues.FirstOrDefault(v => v.FieldId == fieldId);
            if (value is null)
            {
                if (existing is not null)
                    profile.FieldValues.Remove(existing);
            }
            else if (existing is not null)
            {
                existing.Value = value;
            }
            else
            {
                profile.FieldValues.Add(new ProfileFieldValue { UserId = profile.UserId, FieldId = fieldId, Value = value });
            }
        }
    }

    // Rejects display-name forms ("Bob <bob@x.de>") by requiring the parsed address to equal the input.
    private static bool IsValidEmail(string email)
        => email.Length <= MaxEmailLength
           && MailAddress.TryCreate(email, out var address)
           && address.Address == email;

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

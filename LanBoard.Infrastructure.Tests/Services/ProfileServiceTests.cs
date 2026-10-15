using LanBoard.Application.Authorization;
using LanBoard.Application.Errors;
using LanBoard.Application.Interfaces;
using LanBoard.Application.Notifications;
using LanBoard.Application.Profiles;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.Services;
using NSubstitute;

namespace LanBoard.Infrastructure.Tests.Services;

public class ProfileServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUserProfileRepository _profiles = Substitute.For<IUserProfileRepository>();
    private readonly IProfileFieldRepository _fields = Substitute.For<IProfileFieldRepository>();
    private readonly IRsvpRepository _rsvps = Substitute.For<IRsvpRepository>();
    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly ILanPartyRepository _parties = Substitute.For<ILanPartyRepository>();
    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();
    private readonly ProfileService _sut;

    private readonly List<ProfileFieldDefinition> _definitions = [];

    public ProfileServiceTests()
    {
        _sut = new ProfileService(_users, _profiles, _fields, _rsvps, _sessions, _parties, _permissions);
        _fields.GetAllAsync(Arg.Any<CancellationToken>()).Returns(_ => _definitions.OrderBy(d => d.SortOrder).ToList());
        _permissions.GetEffectivePermissionsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>());
        _rsvps.GetAttendedPastPartiesAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns([]);
        _sessions.GetByUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private User GivenUser(string steamName = "SteamGuy", string? steamId = null)
    {
        var user = new User { Id = Guid.NewGuid(), DisplayName = steamName, SteamName = steamName };
        if (steamId is not null)
            user.Identities.Add(new UserIdentity { Provider = "Steam", ProviderUserId = steamId });
        _users.GetWithIdentitiesAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    private UserProfile GivenProfile(User user, Action<UserProfile>? configure = null)
    {
        var profile = new UserProfile { UserId = user.Id, User = user };
        configure?.Invoke(profile);
        _profiles.GetAsync(user.Id, Arg.Any<CancellationToken>()).Returns(profile);
        _profiles.GetForUpdateAsync(user.Id, Arg.Any<CancellationToken>()).Returns(profile);
        return profile;
    }

    private ProfileFieldDefinition GivenField(
        string key,
        ProfileFieldType type = ProfileFieldType.Text,
        FieldVisibility visibility = FieldVisibility.Public,
        bool isRequired = false,
        int sortOrder = 0,
        params string[] choices)
    {
        var field = new ProfileFieldDefinition
        {
            Id = Guid.NewGuid(),
            Key = key,
            Labels = new() { ["en"] = key },
            Type = type,
            Visibility = visibility,
            IsRequired = isRequired,
            SortOrder = sortOrder,
            Choices = [.. choices]
        };
        _definitions.Add(field);
        return field;
    }

    private void GivenViewerPermissions(Guid viewerId, params string[] permissions)
        => _permissions.GetEffectivePermissionsAsync(viewerId, Arg.Any<CancellationToken>())
            .Returns(permissions.ToHashSet());

    private static ProfileSettingsInput Input(
        string? nickname = null,
        string? realName = null,
        FieldVisibility realNameVisibility = FieldVisibility.Orga,
        string? clan = null,
        IReadOnlyList<string>? favoriteGames = null,
        string? email = null,
        IReadOnlySet<string>? optIns = null,
        Dictionary<Guid, string?>? values = null,
        IReadOnlySet<string>? inAppOptOuts = null,
        bool soundEnabled = true)
        => new(nickname, realName, realNameVisibility, clan, favoriteGames ?? [], email,
            optIns ?? new HashSet<string>(), inAppOptOuts ?? new HashSet<string>(), soundEnabled, values ?? []);

    private async Task<ValidationException> AssertInvalidAsync(Guid userId, ProfileSettingsInput input)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.SaveSettingsAsync(userId, input));
        await _profiles.DidNotReceive().AddAsync(Arg.Any<UserProfile>(), Arg.Any<CancellationToken>());
        await _profiles.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        return ex;
    }

    private static void AssertSingleError(ValidationException ex, string field, string code)
    {
        var error = Assert.Single(ex.Errors);
        Assert.Equal(field, error.Field);
        Assert.Equal(code, error.Code);
    }

    // --- GetProfileAsync ---

    [Fact]
    public async Task GetProfileAsync_UnknownUser_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.GetProfileAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
    }

    [Fact]
    public async Task GetProfileAsync_WithoutProfileRow_ReturnsDefaults()
    {
        var user = GivenUser(steamId: "76561198000000001");

        var view = await _sut.GetProfileAsync(user.Id, Guid.NewGuid());

        Assert.Equal("SteamGuy", view.DisplayName);
        Assert.Equal("https://steamcommunity.com/profiles/76561198000000001", view.SteamProfileUrl);
        Assert.Null(view.RealName);
        Assert.Null(view.Clan);
        Assert.Empty(view.FavoriteGames);
        Assert.Empty(view.CustomFields);
        Assert.False(view.IsOwnProfile);
    }

    [Fact]
    public async Task GetProfileAsync_PublicFields_AlwaysShown_EmailNeverExposed()
    {
        var user = GivenUser();
        GivenProfile(user, p =>
        {
            p.Clan = "[LAN]";
            p.FavoriteGames = ["Quake", "CS2"];
            p.ContactEmail = "secret@example.com";
        });

        var view = await _sut.GetProfileAsync(user.Id, Guid.NewGuid());

        Assert.Equal("[LAN]", view.Clan);
        Assert.Equal(["Quake", "CS2"], view.FavoriteGames);
        Assert.DoesNotContain(typeof(ProfileView).GetProperties(), p => p.Name.Contains("Email"));
    }

    [Theory]
    [InlineData(FieldVisibility.Public, false, true)]
    [InlineData(FieldVisibility.Orga, false, false)]
    [InlineData(FieldVisibility.Orga, true, true)]
    [InlineData(FieldVisibility.Private, true, false)]
    public async Task GetProfileAsync_RealName_FilteredByVisibility(FieldVisibility visibility, bool viewerHasProfileRead, bool visible)
    {
        var user = GivenUser();
        GivenProfile(user, p => { p.RealName = "Max Mustermann"; p.RealNameVisibility = visibility; });
        var viewerId = Guid.NewGuid();
        if (viewerHasProfileRead)
            GivenViewerPermissions(viewerId, Permissions.ProfileRead);

        var view = await _sut.GetProfileAsync(user.Id, viewerId);

        Assert.Equal(visible ? "Max Mustermann" : null, view.RealName);
    }

    [Fact]
    public async Task GetProfileAsync_OwnProfile_SeesPrivateRealName()
    {
        var user = GivenUser();
        GivenProfile(user, p => { p.RealName = "Max"; p.RealNameVisibility = FieldVisibility.Private; });

        var view = await _sut.GetProfileAsync(user.Id, user.Id);

        Assert.Equal("Max", view.RealName);
        Assert.True(view.IsOwnProfile);
    }

    [Fact]
    public async Task GetProfileAsync_CustomFields_FilteredByVisibilityAndSorted()
    {
        var user = GivenUser();
        var discord = GivenField("discord", sortOrder: 2);
        var shirt = GivenField("shirt", visibility: FieldVisibility.Orga, sortOrder: 3);
        var diet = GivenField("diet", visibility: FieldVisibility.Private, sortOrder: 4);
        var steamDeck = GivenField("steam-deck", ProfileFieldType.YesNo, sortOrder: 1);
        var empty = GivenField("empty", sortOrder: 0);
        GivenProfile(user, p => p.FieldValues =
        [
            new ProfileFieldValue { UserId = user.Id, FieldId = discord.Id, Value = "max#1" },
            new ProfileFieldValue { UserId = user.Id, FieldId = shirt.Id, Value = "XL" },
            new ProfileFieldValue { UserId = user.Id, FieldId = diet.Id, Value = "vegan" },
            new ProfileFieldValue { UserId = user.Id, FieldId = steamDeck.Id, Value = "yes" },
        ]);
        var viewerId = Guid.NewGuid();
        GivenViewerPermissions(viewerId, Permissions.ProfileRead);

        var view = await _sut.GetProfileAsync(user.Id, viewerId);

        Assert.Equal(["steam-deck", "discord", "shirt"], view.CustomFields.Select(f => f.Key));
        Assert.Equal("XL", view.CustomFields.Single(f => f.FieldId == shirt.Id).Value);
        Assert.DoesNotContain(view.CustomFields, f => f.FieldId == empty.Id);
    }

    [Fact]
    public async Task GetProfileAsync_PastParties_ComeFromRepositoryWithTodayAsCutoff()
    {
        var user = GivenUser();
        var party = new LanParty { Id = Guid.NewGuid(), Name = "LAN 1", Location = new Location { Name = "Hall" }, Date = new DateTime(2026, 1, 1) };
        _rsvps.GetAttendedPastPartiesAsync(user.Id, DateTime.UtcNow.Date, Arg.Any<CancellationToken>()).Returns([party]);

        var view = await _sut.GetProfileAsync(user.Id, Guid.NewGuid());

        var past = Assert.Single(view.PastParties);
        Assert.Equal(new PastPartyView(party.Id, "LAN 1", party.Date, "Hall"), past);
    }

    [Fact]
    public async Task GetProfileAsync_PlayedGames_AggregatedAcrossPartiesByPlaytime()
    {
        var user = GivenUser();
        var party1 = Guid.NewGuid();
        var party2 = Guid.NewGuid();
        var start = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        Session S(Guid partyId, string? game, int minutes)
            => new() { Id = Guid.NewGuid(), UserId = user.Id, PartyId = partyId, GameName = game, JoinedAt = start, LastSeen = start.AddMinutes(minutes) };
        _sessions.GetByUserAsync(user.Id, Arg.Any<CancellationToken>()).Returns(
        [
            S(party1, "Quake", 30), S(party2, "Quake", 60), S(party1, "CS2", 120), S(party1, null, 500)
        ]);

        var view = await _sut.GetProfileAsync(user.Id, Guid.NewGuid());

        Assert.Equal(
            [new PlayedGameView("CS2", TimeSpan.FromMinutes(120), 1), new PlayedGameView("Quake", TimeSpan.FromMinutes(90), 2)],
            view.PlayedGames);
    }

    // --- GetSettingsAsync ---

    [Fact]
    public async Task GetSettingsAsync_ReturnsOwnDataIncludingEmailAndAllFields()
    {
        var user = GivenUser();
        user.SetNickname("Nick");
        var privateField = GivenField("diet", visibility: FieldVisibility.Private);
        GivenProfile(user, p =>
        {
            p.ContactEmail = "me@example.com";
            p.NotificationOptIns = [NotificationCategories.PartyAnnouncements];
            p.FieldValues = [new ProfileFieldValue { UserId = user.Id, FieldId = privateField.Id, Value = "vegan" }];
        });

        var settings = await _sut.GetSettingsAsync(user.Id);

        Assert.Equal("SteamGuy", settings.SteamName);
        Assert.Equal("Nick", settings.Nickname);
        Assert.Equal("me@example.com", settings.ContactEmail);
        Assert.Equal([NotificationCategories.PartyAnnouncements], settings.NotificationOptIns);
        Assert.Single(settings.Fields);
        Assert.Equal("vegan", settings.FieldValues[privateField.Id]);
    }

    [Fact]
    public async Task GetSettingsAsync_WithoutProfileRow_DefaultsToOrgaAndNoOptIns()
    {
        var user = GivenUser();

        var settings = await _sut.GetSettingsAsync(user.Id);

        Assert.Equal(FieldVisibility.Orga, settings.RealNameVisibility);
        Assert.Empty(settings.NotificationOptIns);
        Assert.Null(settings.ContactEmail);
        Assert.Empty(settings.InAppOptOuts);
        Assert.True(settings.NotificationSoundEnabled);
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsInAppPreferences()
    {
        var user = GivenUser();
        GivenProfile(user, p =>
        {
            p.InAppOptOuts = [NotificationCategories.AttendeeOnline];
            p.NotificationSoundEnabled = false;
        });

        var settings = await _sut.GetSettingsAsync(user.Id);

        Assert.Equal([NotificationCategories.AttendeeOnline], settings.InAppOptOuts);
        Assert.False(settings.NotificationSoundEnabled);
    }

    // --- SaveSettingsAsync: happy paths ---

    [Fact]
    public async Task SaveSettingsAsync_WithoutProfileRow_CreatesProfileAndSavesEverything()
    {
        var user = GivenUser();
        var discord = GivenField("discord");

        await _sut.SaveSettingsAsync(user.Id, Input(
            nickname: " Fragmaster ",
            realName: " Max ",
            realNameVisibility: FieldVisibility.Public,
            clan: " [LAN] ",
            favoriteGames: [" Quake ", "quake", "", "CS2"],
            email: " max@example.com ",
            optIns: new HashSet<string> { NotificationCategories.TournamentUpdates },
            values: new() { [discord.Id] = " max#1 " }));

        Assert.Equal("Fragmaster", user.DisplayName);
        await _profiles.Received(1).AddAsync(Arg.Is<UserProfile>(p =>
            p.UserId == user.Id
            && p.RealName == "Max"
            && p.RealNameVisibility == FieldVisibility.Public
            && p.Clan == "[LAN]"
            && p.FavoriteGames.SequenceEqual(new[] { "Quake", "CS2" })
            && p.ContactEmail == "max@example.com"
            && p.NotificationOptIns.SequenceEqual(new[] { NotificationCategories.TournamentUpdates })
            && p.FieldValues.Single().Value == "max#1"
            && p.UpdatedAt != default), Arg.Any<CancellationToken>());
        await _profiles.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveSettingsAsync_ExistingProfile_UpdatesInPlace()
    {
        var user = GivenUser();
        var profile = GivenProfile(user, p => p.Clan = "Old");

        await _sut.SaveSettingsAsync(user.Id, Input(clan: "New"));

        Assert.Equal("New", profile.Clan);
        await _profiles.DidNotReceive().AddAsync(Arg.Any<UserProfile>(), Arg.Any<CancellationToken>());
        await _profiles.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveSettingsAsync_ClearedNickname_FallsBackToSteamName(string? nickname)
    {
        var user = GivenUser();
        user.SetNickname("Fragmaster");

        await _sut.SaveSettingsAsync(user.Id, Input(nickname: nickname));

        Assert.Null(user.Nickname);
        Assert.Equal("SteamGuy", user.DisplayName);
    }

    [Fact]
    public async Task SaveSettingsAsync_EmptyCustomValue_DeletesRow_OtherValuesUntouched()
    {
        var user = GivenUser();
        var discord = GivenField("discord");
        var shirt = GivenField("shirt");
        var profile = GivenProfile(user, p => p.FieldValues =
        [
            new ProfileFieldValue { UserId = user.Id, FieldId = discord.Id, Value = "max#1" },
            new ProfileFieldValue { UserId = user.Id, FieldId = shirt.Id, Value = "XL" },
        ]);

        await _sut.SaveSettingsAsync(user.Id, Input(values: new() { [discord.Id] = "  " }));

        var remaining = Assert.Single(profile.FieldValues);
        Assert.Equal(shirt.Id, remaining.FieldId);
        Assert.Equal("XL", remaining.Value);
    }

    [Fact]
    public async Task SaveSettingsAsync_ExistingCustomValue_IsUpdated()
    {
        var user = GivenUser();
        var shirt = GivenField("shirt", ProfileFieldType.SingleChoice, choices: ["M", "XL"]);
        var profile = GivenProfile(user, p => p.FieldValues = [new ProfileFieldValue { UserId = user.Id, FieldId = shirt.Id, Value = "M" }]);

        await _sut.SaveSettingsAsync(user.Id, Input(values: new() { [shirt.Id] = "XL" }));

        Assert.Equal("XL", Assert.Single(profile.FieldValues).Value);
    }

    [Fact]
    public async Task SaveSettingsAsync_UnchangedNickname_NotCheckedForUniquenessAgain()
    {
        var user = GivenUser();
        user.SetNickname("Fragmaster");
        _users.DisplayNameTakenAsync("Fragmaster", user.Id, Arg.Any<CancellationToken>()).Returns(true);

        await _sut.SaveSettingsAsync(user.Id, Input(nickname: "Fragmaster"));

        await _profiles.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // --- SaveSettingsAsync: validation ---

    [Fact]
    public async Task SaveSettingsAsync_UnknownUser_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.SaveSettingsAsync(Guid.NewGuid(), Input()));

        Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
    }

    [Fact]
    public async Task SaveSettingsAsync_NicknameTooLong_Rejected()
    {
        var user = GivenUser();

        var ex = await AssertInvalidAsync(user.Id, Input(nickname: new string('x', 33)));

        AssertSingleError(ex, "Nickname", ErrorCodes.ProfileNicknameInvalid);
        Assert.Equal([32], ex.Errors[0].Args);
        Assert.Equal("SteamGuy", user.DisplayName);
    }

    [Fact]
    public async Task SaveSettingsAsync_NicknameTaken_Rejected()
    {
        var user = GivenUser();
        _users.DisplayNameTakenAsync("alice", user.Id, Arg.Any<CancellationToken>()).Returns(true);

        var ex = await AssertInvalidAsync(user.Id, Input(nickname: " alice "));

        AssertSingleError(ex, "Nickname", ErrorCodes.ProfileNicknameTaken);
        Assert.Null(user.Nickname);
    }

    [Fact]
    public async Task SaveSettingsAsync_TooLongTexts_Rejected()
    {
        var user = GivenUser();

        var ex = await AssertInvalidAsync(user.Id, Input(realName: new string('x', 101), clan: new string('x', 51)));

        Assert.Equal(
            [("RealName", ErrorCodes.ProfileValueTooLong, 100), ("Clan", ErrorCodes.ProfileValueTooLong, 50)],
            ex.Errors.Select(e => (e.Field, e.Code, (int)e.Args[0])));
    }

    [Fact]
    public async Task SaveSettingsAsync_TooManyFavoriteGames_Rejected()
    {
        var user = GivenUser();

        var ex = await AssertInvalidAsync(user.Id, Input(favoriteGames: Enumerable.Range(1, 11).Select(i => $"Game {i}").ToList()));

        AssertSingleError(ex, "FavoriteGames", ErrorCodes.ProfileTooManyFavoriteGames);
    }

    [Fact]
    public async Task SaveSettingsAsync_DuplicateFavoriteGames_CountOnce()
    {
        var user = GivenUser();
        var games = Enumerable.Range(1, 10).Select(i => $"Game {i}").Concat(["game 1", "GAME 2"]).ToList();

        await _sut.SaveSettingsAsync(user.Id, Input(favoriteGames: games));

        await _profiles.Received(1).AddAsync(Arg.Is<UserProfile>(p => p.FavoriteGames.Count == 10), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveSettingsAsync_FavoriteGameTooLong_Rejected()
    {
        var user = GivenUser();

        var ex = await AssertInvalidAsync(user.Id, Input(favoriteGames: [new string('x', 51)]));

        AssertSingleError(ex, "FavoriteGames", ErrorCodes.ProfileValueTooLong);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("Max <max@example.com>")]
    [InlineData("max@")]
    public async Task SaveSettingsAsync_InvalidEmail_Rejected(string email)
    {
        var user = GivenUser();

        var ex = await AssertInvalidAsync(user.Id, Input(email: email));

        AssertSingleError(ex, "ContactEmail", ErrorCodes.ProfileEmailInvalid);
    }

    [Fact]
    public async Task SaveSettingsAsync_EmailTooLong_Rejected()
    {
        var user = GivenUser();

        var ex = await AssertInvalidAsync(user.Id, Input(email: new string('a', 243) + "@example.com")); // 255 chars

        AssertSingleError(ex, "ContactEmail", ErrorCodes.ProfileEmailInvalid);
    }

    [Fact]
    public async Task SaveSettingsAsync_EmptyEmail_RemovesIt()
    {
        var user = GivenUser();
        var profile = GivenProfile(user, p => p.ContactEmail = "old@example.com");

        await _sut.SaveSettingsAsync(user.Id, Input(email: " "));

        Assert.Null(profile.ContactEmail);
    }

    [Fact]
    public async Task SaveSettingsAsync_UnknownNotificationCategory_Rejected()
    {
        var user = GivenUser();

        var ex = await AssertInvalidAsync(user.Id, Input(optIns: new HashSet<string> { "spam.everything" }));

        AssertSingleError(ex, "NotificationOptIns", ErrorCodes.ProfileUnknownCategory);
        Assert.Equal(["spam.everything"], ex.Errors[0].Args);
    }

    [Fact]
    public async Task SaveSettingsAsync_InAppOnlyCategoryAsEmailOptIn_Rejected()
    {
        var user = GivenUser();

        var ex = await AssertInvalidAsync(user.Id, Input(optIns: new HashSet<string> { NotificationCategories.AttendeeOnline }));

        AssertSingleError(ex, "NotificationOptIns", ErrorCodes.ProfileUnknownCategory);
    }

    [Fact]
    public async Task SaveSettingsAsync_UnknownInAppCategory_Rejected()
    {
        var user = GivenUser();

        var ex = await AssertInvalidAsync(user.Id, Input(inAppOptOuts: new HashSet<string> { NotificationCategories.PartyAnnouncements }));

        AssertSingleError(ex, "InAppOptOuts", ErrorCodes.ProfileUnknownCategory);
        Assert.Equal([NotificationCategories.PartyAnnouncements], ex.Errors[0].Args);
    }

    [Fact]
    public async Task SaveSettingsAsync_InAppPreferences_Saved()
    {
        var user = GivenUser();
        var profile = GivenProfile(user);

        await _sut.SaveSettingsAsync(user.Id, Input(
            inAppOptOuts: new HashSet<string> { NotificationCategories.AttendeeOnline },
            soundEnabled: false));

        Assert.Equal([NotificationCategories.AttendeeOnline], profile.InAppOptOuts);
        Assert.False(profile.NotificationSoundEnabled);

        await _sut.SaveSettingsAsync(user.Id, Input());

        Assert.Empty(profile.InAppOptOuts);
        Assert.True(profile.NotificationSoundEnabled);
    }

    [Fact]
    public async Task SaveSettingsAsync_UnknownCustomField_Rejected()
    {
        var user = GivenUser();
        var unknownId = Guid.NewGuid();

        var ex = await AssertInvalidAsync(user.Id, Input(values: new() { [unknownId] = "x" }));

        AssertSingleError(ex, $"custom:{unknownId}", ErrorCodes.ProfileFieldNotFound);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task SaveSettingsAsync_RequiredFieldEmpty_Rejected(string? value)
    {
        var user = GivenUser();
        var shirt = GivenField("shirt", isRequired: true);

        var ex = await AssertInvalidAsync(user.Id, Input(values: new() { [shirt.Id] = value }));

        AssertSingleError(ex, $"custom:{shirt.Id}", ErrorCodes.ProfileFieldRequired);
    }

    [Fact]
    public async Task SaveSettingsAsync_RequiredFieldNotSubmittedAndNotStored_Rejected()
    {
        var user = GivenUser();
        var shirt = GivenField("shirt", isRequired: true);

        var ex = await AssertInvalidAsync(user.Id, Input());

        AssertSingleError(ex, $"custom:{shirt.Id}", ErrorCodes.ProfileFieldRequired);
    }

    [Fact]
    public async Task SaveSettingsAsync_RequiredFieldNotSubmittedButStored_Accepted()
    {
        var user = GivenUser();
        var shirt = GivenField("shirt", isRequired: true);
        GivenProfile(user, p => p.FieldValues = [new ProfileFieldValue { UserId = user.Id, FieldId = shirt.Id, Value = "XL" }]);

        await _sut.SaveSettingsAsync(user.Id, Input());

        await _profiles.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveSettingsAsync_TextValueTooLong_Rejected()
    {
        var user = GivenUser();
        var discord = GivenField("discord");

        var ex = await AssertInvalidAsync(user.Id, Input(values: new() { [discord.Id] = new string('x', 201) }));

        AssertSingleError(ex, $"custom:{discord.Id}", ErrorCodes.ProfileValueTooLong);
    }

    [Theory]
    [InlineData("xl")]
    [InlineData("XXL")]
    public async Task SaveSettingsAsync_SingleChoiceNotInList_Rejected(string value)
    {
        var user = GivenUser();
        var shirt = GivenField("shirt", ProfileFieldType.SingleChoice, choices: ["M", "XL"]);

        var ex = await AssertInvalidAsync(user.Id, Input(values: new() { [shirt.Id] = value }));

        AssertSingleError(ex, $"custom:{shirt.Id}", ErrorCodes.ProfileFieldInvalidChoice);
    }

    [Theory]
    [InlineData("yes", true)]
    [InlineData("no", true)]
    [InlineData("Yes", false)]
    [InlineData("maybe", false)]
    public async Task SaveSettingsAsync_YesNoValues(string value, bool valid)
    {
        var user = GivenUser();
        var deck = GivenField("steam-deck", ProfileFieldType.YesNo);
        var input = Input(values: new() { [deck.Id] = value });

        if (valid)
        {
            await _sut.SaveSettingsAsync(user.Id, input);
            await _profiles.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }
        else
        {
            var ex = await AssertInvalidAsync(user.Id, input);
            AssertSingleError(ex, $"custom:{deck.Id}", ErrorCodes.ProfileFieldInvalidChoice);
        }
    }

    [Fact]
    public async Task SaveSettingsAsync_MultipleErrors_ReportedTogether_NothingChanged()
    {
        var user = GivenUser();
        var shirt = GivenField("shirt", ProfileFieldType.SingleChoice, isRequired: true, choices: ["M"]);
        var profile = GivenProfile(user, p => p.Clan = "Old");

        var ex = await AssertInvalidAsync(user.Id, Input(
            nickname: new string('x', 40),
            clan: "New",
            email: "nope",
            values: new() { [shirt.Id] = "XXL" }));

        Assert.Equal(
            ["Nickname", "ContactEmail", $"custom:{shirt.Id}"],
            ex.Errors.Select(e => e.Field));
        Assert.Equal("Old", profile.Clan);
        Assert.Null(user.Nickname);
    }

    // --- GetPartyOverviewAsync ---

    [Fact]
    public async Task GetPartyOverviewAsync_WithoutPermission_Throws()
    {
        var callerId = Guid.NewGuid();
        _permissions.EnsureAsync(callerId, Permissions.ProfileRead, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new PermissionDeniedException(Permissions.ProfileRead)));

        await Assert.ThrowsAsync<PermissionDeniedException>(() => _sut.GetPartyOverviewAsync(Guid.NewGuid(), callerId));

        await _rsvps.DidNotReceive().GetAttendingUsersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPartyOverviewAsync_UnknownParty_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.GetPartyOverviewAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(ErrorCodes.PartyNotFound, ex.Code);
    }

    [Fact]
    public async Task GetPartyOverviewAsync_AttendeesWithPublicAndOrgaColumnsOnly()
    {
        var party = new LanParty { Id = Guid.NewGuid(), Name = "LAN", Location = new Location { Name = "Hall" } };
        _parties.GetByIdAsync(party.Id, Arg.Any<CancellationToken>()).Returns(party);
        var discord = GivenField("discord", sortOrder: 1);
        var shirt = GivenField("shirt", visibility: FieldVisibility.Orga, sortOrder: 2);
        var diet = GivenField("diet", visibility: FieldVisibility.Private, sortOrder: 3);
        var alice = new User { Id = Guid.NewGuid(), DisplayName = "Alice", SteamName = "Alice" };
        var bob = new User { Id = Guid.NewGuid(), DisplayName = "Bob", SteamName = "Bob" };
        _rsvps.GetAttendingUsersAsync(party.Id, Arg.Any<CancellationToken>()).Returns([alice, bob]);
        _profiles.GetByUserIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { alice.Id, bob.Id })), Arg.Any<CancellationToken>())
            .Returns([
                new UserProfile
                {
                    UserId = alice.Id,
                    FieldValues =
                    [
                        new ProfileFieldValue { UserId = alice.Id, FieldId = shirt.Id, Value = "M" },
                        new ProfileFieldValue { UserId = alice.Id, FieldId = diet.Id, Value = "vegan" },
                    ]
                }
            ]);

        var overview = await _sut.GetPartyOverviewAsync(party.Id, Guid.NewGuid());

        await _permissions.Received(1).EnsureAsync(Arg.Any<Guid>(), Permissions.ProfileRead, Arg.Any<CancellationToken>());
        Assert.Equal([discord.Id, shirt.Id], overview.Columns.Select(c => c.Id));
        Assert.Equal(["Alice", "Bob"], overview.Rows.Select(r => r.DisplayName));
        Assert.Equal(new Dictionary<Guid, string> { [shirt.Id] = "M" }, overview.Rows[0].Values);
        Assert.Empty(overview.Rows[1].Values);
    }
}

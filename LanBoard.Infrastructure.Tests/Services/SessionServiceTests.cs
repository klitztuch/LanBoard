using LanBoard.Application.Interfaces;
using LanBoard.Application.Notifications;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.ApiClients.Steam;
using LanBoard.Infrastructure.ApiClients.Steam.Models;
using LanBoard.Infrastructure.Services;
using NSubstitute;

namespace LanBoard.Infrastructure.Tests.Services;

public class SessionServiceTests
{
    private static readonly Guid PartyId = Guid.NewGuid();

    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly ISeatRepository _seats = Substitute.For<ISeatRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ISteamApiClient _steamApiClient = Substitute.For<ISteamApiClient>();
    private readonly ILanBoardNotifier _notifier = Substitute.For<ILanBoardNotifier>();

    private readonly SessionService _sut;

    public SessionServiceTests()
    {
        _sut = new SessionService(_sessions, _seats, _users, _steamApiClient, _notifier);
    }

    private static User CreateSeatedUser(string? steamId = "76561198000000001")
    {
        var user = new User { Id = Guid.NewGuid(), DisplayName = "Player", CreatedAt = DateTime.UtcNow };
        if (steamId is not null)
        {
            user.Identities.Add(new UserIdentity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Provider = "Steam",
                ProviderUserId = steamId,
                CreatedAt = DateTime.UtcNow
            });
        }

        return user;
    }

    private void SetSeatedUsers(params User[] users)
    {
        var seats = users.Select(u => new Seat
        {
            Id = Guid.NewGuid(),
            PartyId = PartyId,
            Label = "Seat",
            AssignedUserId = u.Id
        }).ToArray();

        _seats.GetByPartyAsync(PartyId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Seat>)seats);
        _users.GetAllWithIdentitiesAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<User>)users);
    }

    private void SetActiveSessions(params Session[] sessions)
        => _sessions.GetActiveByPartyAsync(PartyId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Session>)sessions);

    private void SetSteamSummaries(params SteamPlayerSummary[] summaries)
        => _steamApiClient.GetPlayerSummariesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SteamPlayerSummary>)summaries);

    [Fact]
    public async Task NoActiveSession_CreatesNewSessionAndNotifies()
    {
        var user = CreateSeatedUser();
        SetSeatedUsers(user);
        SetActiveSessions(); // none active — either the user just joined or the previous row fell outside the freshness window
        SetSteamSummaries(new SteamPlayerSummary(user.Identities.First().ProviderUserId, null, "440", "Team Fortress 2"));

        await _sut.SyncActiveSessionsAsync(PartyId);

        await _sessions.Received(1).AddAsync(
            Arg.Is<Session>(s => s.UserId == user.Id && s.GameAppId == "440"),
            Arg.Any<CancellationToken>());
        await _sessions.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _notifier.Received(1).NotifyChanged();
    }

    [Fact]
    public async Task GameSwitch_KeepsOldSessionRowAndCreatesNewOneAndNotifies()
    {
        var user = CreateSeatedUser();
        SetSeatedUsers(user);

        var previousSession = new Session
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PartyId = PartyId,
            GameAppId = "570",
            GameName = "Dota 2",
            JoinedAt = DateTime.UtcNow.AddMinutes(-10),
            LastSeen = DateTime.UtcNow.AddSeconds(-5)
        };
        SetActiveSessions(previousSession);
        SetSteamSummaries(new SteamPlayerSummary(user.Identities.First().ProviderUserId, null, "440", "Team Fortress 2"));

        await _sut.SyncActiveSessionsAsync(PartyId);

        // The old row is left untouched — a new row is added for the new game instead of updating it in place.
        Assert.Equal("570", previousSession.GameAppId);
        await _sessions.Received(1).AddAsync(
            Arg.Is<Session>(s => s.UserId == user.Id && s.GameAppId == "440"),
            Arg.Any<CancellationToken>());
        await _sessions.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _notifier.Received(1).NotifyChanged();
    }

    [Fact]
    public async Task SameGameStillRunning_OnlyUpdatesLastSeenAndDoesNotNotify()
    {
        var user = CreateSeatedUser();
        SetSeatedUsers(user);

        var existingSession = new Session
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PartyId = PartyId,
            GameAppId = "440",
            GameName = "Team Fortress 2",
            JoinedAt = DateTime.UtcNow.AddMinutes(-10),
            LastSeen = DateTime.UtcNow.AddSeconds(-30)
        };
        SetActiveSessions(existingSession);
        SetSteamSummaries(new SteamPlayerSummary(user.Identities.First().ProviderUserId, null, "440", "Team Fortress 2"));

        var before = existingSession.LastSeen;

        await _sut.SyncActiveSessionsAsync(PartyId);

        Assert.True(existingSession.LastSeen > before);
        await _sessions.DidNotReceive().AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
        await _sessions.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _notifier.DidNotReceive().NotifyChanged();
    }

    [Fact]
    public async Task UserWithoutSteamIdentity_IsSkippedWithoutError()
    {
        var user = CreateSeatedUser(steamId: null);
        SetSeatedUsers(user);
        SetActiveSessions();

        await _sut.SyncActiveSessionsAsync(PartyId);

        await _steamApiClient.DidNotReceive().GetPlayerSummariesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        await _sessions.DidNotReceive().AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
        await _sessions.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _notifier.DidNotReceive().NotifyChanged();
    }

    [Fact]
    public async Task GetActiveSessionsByPartyAsync_UsesNoTrackingDisplayQuery_GroupedByUser()
    {
        var userId = Guid.NewGuid();
        var session = new Session { Id = Guid.NewGuid(), UserId = userId, PartyId = PartyId, JoinedAt = DateTime.UtcNow, LastSeen = DateTime.UtcNow };
        _sessions.GetActiveByPartyForDisplayAsync(PartyId, Arg.Any<CancellationToken>()).Returns([session]);

        var result = await _sut.GetActiveSessionsByPartyAsync(PartyId);

        Assert.Same(session, result[userId]);
        await _sessions.DidNotReceive().GetActiveByPartyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoSeatedUsers_ReturnsEarlyWithoutCallingSteamApi()
    {
        SetSeatedUsers();

        await _sut.SyncActiveSessionsAsync(PartyId);

        await _steamApiClient.DidNotReceive().GetPlayerSummariesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        _notifier.DidNotReceive().NotifyChanged();
    }
}

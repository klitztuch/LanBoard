using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.Services;
using NSubstitute;

namespace LanBoard.Infrastructure.Tests.Services;

public class PartyStatsServiceTests
{
    private static readonly Guid PartyId = Guid.NewGuid();

    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly PartyStatsService _sut;

    public PartyStatsServiceTests()
    {
        _sut = new PartyStatsService(_sessions);
    }

    private static Session CreateSession(User user, string? gameName, TimeSpan duration)
    {
        var joinedAt = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        return new Session
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PartyId = PartyId,
            GameName = gameName,
            GameAppId = gameName is null ? null : "1",
            JoinedAt = joinedAt,
            LastSeen = joinedAt + duration,
            User = user
        };
    }

    private static User CreateUser(string name) => new() { Id = Guid.NewGuid(), DisplayName = name };

    [Fact]
    public async Task GetStatsAsync_NoSessions_ReturnsEmptyStats()
    {
        _sessions.GetByPartyAsync(PartyId, Arg.Any<CancellationToken>()).Returns([]);

        var result = await _sut.GetStatsAsync(PartyId);

        Assert.Empty(result.TopGames);
        Assert.Empty(result.TopUsers);
        Assert.Equal(TimeSpan.Zero, result.TotalPlaytime);
    }

    [Fact]
    public async Task GetStatsAsync_AggregatesPlaytimePerGame()
    {
        var alice = CreateUser("Alice");
        var bob = CreateUser("Bob");
        var sessions = new List<Session>
        {
            CreateSession(alice, "Factorio", TimeSpan.FromHours(2)),
            CreateSession(bob, "Factorio", TimeSpan.FromHours(1)),
            CreateSession(alice, "Chess", TimeSpan.FromMinutes(30))
        };
        _sessions.GetByPartyAsync(PartyId, Arg.Any<CancellationToken>()).Returns(sessions);

        var result = await _sut.GetStatsAsync(PartyId);

        var factorio = Assert.Single(result.TopGames, g => g.GameName == "Factorio");
        Assert.Equal(TimeSpan.FromHours(3), factorio.TotalPlaytime);
        Assert.Equal(2, factorio.PlayerCount);
        Assert.Equal(TimeSpan.FromHours(3.5), result.TotalPlaytime);
    }

    [Fact]
    public async Task GetStatsAsync_AggregatesPlaytimePerUser_OrderedDescending()
    {
        var alice = CreateUser("Alice");
        var bob = CreateUser("Bob");
        var sessions = new List<Session>
        {
            CreateSession(alice, "Factorio", TimeSpan.FromHours(1)),
            CreateSession(bob, "Chess", TimeSpan.FromHours(3))
        };
        _sessions.GetByPartyAsync(PartyId, Arg.Any<CancellationToken>()).Returns(sessions);

        var result = await _sut.GetStatsAsync(PartyId);

        Assert.Equal("Bob", result.TopUsers[0].DisplayName);
        Assert.Equal(TimeSpan.FromHours(3), result.TopUsers[0].TotalPlaytime);
        Assert.Equal("Alice", result.TopUsers[1].DisplayName);
    }

    [Fact]
    public async Task GetStatsAsync_SessionsWithoutGameName_ExcludedFromTopGamesButCountTowardsUserAndTotal()
    {
        var alice = CreateUser("Alice");
        var sessions = new List<Session> { CreateSession(alice, null, TimeSpan.FromMinutes(15)) };
        _sessions.GetByPartyAsync(PartyId, Arg.Any<CancellationToken>()).Returns(sessions);

        var result = await _sut.GetStatsAsync(PartyId);

        Assert.Empty(result.TopGames);
        Assert.Equal(TimeSpan.FromMinutes(15), result.TopUsers[0].TotalPlaytime);
        Assert.Equal(TimeSpan.FromMinutes(15), result.TotalPlaytime);
    }
}

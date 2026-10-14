using LanBoard.Application.Errors;
using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.Services;
using NSubstitute;

namespace LanBoard.Infrastructure.Tests.Services;

public class UserServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _sut = new UserService(_users);
    }

    [Fact]
    public async Task GetAllWithIdentitiesAsync_DelegatesToRepository()
    {
        var expected = new List<User> { new() { Id = Guid.NewGuid(), DisplayName = "Player", SteamName = "Player", CreatedAt = DateTime.UtcNow } };
        _users.GetAllWithIdentitiesAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<User>)expected);

        var result = await _sut.GetAllWithIdentitiesAsync();

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetOrCreateAndSyncSteamProfileAsync_ExistingUser_UpdatesProfileAndSaves()
    {
        var existing = new User { Id = Guid.NewGuid(), DisplayName = "Old Name", SteamName = "Old Name", CreatedAt = DateTime.UtcNow };
        _users.FindByProviderAsync("Steam", "76561198000000001", Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _sut.GetOrCreateAndSyncSteamProfileAsync("76561198000000001", "New Name", "https://avatar/1.jpg");

        Assert.Same(existing, result);
        Assert.Equal("New Name", result.DisplayName);
        Assert.Equal("New Name", result.SteamName);
        Assert.Equal("https://avatar/1.jpg", result.AvatarUrl);
        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _users.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrCreateAndSyncSteamProfileAsync_ExistingUserWithNickname_KeepsNickname()
    {
        var existing = new User { Id = Guid.NewGuid(), DisplayName = "Old Name", SteamName = "Old Name", CreatedAt = DateTime.UtcNow };
        existing.SetNickname("Fragmaster");
        _users.FindByProviderAsync("Steam", "76561198000000001", Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _sut.GetOrCreateAndSyncSteamProfileAsync("76561198000000001", "New Name", "https://avatar/2.jpg");

        Assert.Equal("Fragmaster", result.DisplayName);
        Assert.Equal("Fragmaster", result.Nickname);
        Assert.Equal("New Name", result.SteamName);
        Assert.Equal("https://avatar/2.jpg", result.AvatarUrl);
        await _users.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrCreateAndSyncSteamProfileAsync_NewUser_CreatesUserWithSteamIdentity()
    {
        _users.FindByProviderAsync("Steam", "76561198000000002", Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _sut.GetOrCreateAndSyncSteamProfileAsync("76561198000000002", "Fresh Player", null);

        Assert.Equal("Fresh Player", result.DisplayName);
        Assert.Equal("Fresh Player", result.SteamName);
        Assert.Null(result.Nickname);
        var identity = Assert.Single(result.Identities);
        Assert.Equal("Steam", identity.Provider);
        Assert.Equal("76561198000000002", identity.ProviderUserId);
        await _users.Received(1).AddAsync(result, Arg.Any<CancellationToken>());
        await _users.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetPreferredCultureAsync_ExistingUser_PersistsCulture()
    {
        var user = new User { Id = Guid.NewGuid(), DisplayName = "Player", SteamName = "Player", CreatedAt = DateTime.UtcNow };
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await _sut.SetPreferredCultureAsync(user.Id, "de");

        Assert.Equal("de", user.PreferredCulture);
        await _users.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetPreferredCultureAsync_UnknownUser_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.SetPreferredCultureAsync(Guid.NewGuid(), "de"));

        Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        await _users.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

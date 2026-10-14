using LanBoard.Core.Entities;

namespace LanBoard.Infrastructure.Tests.Entities;

public class UserTests
{
    private static User CreateUser(string steamName = "SteamGuy")
        => new() { Id = Guid.NewGuid(), DisplayName = steamName, SteamName = steamName };

    [Fact]
    public void SetNickname_SetsTrimmedNicknameAsDisplayName()
    {
        var user = CreateUser();

        user.SetNickname("  Fragmaster ");

        Assert.Equal("Fragmaster", user.Nickname);
        Assert.Equal("Fragmaster", user.DisplayName);
        Assert.Equal("SteamGuy", user.SteamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetNickname_NullOrWhitespace_ClearsNicknameAndFallsBackToSteamName(string? nickname)
    {
        var user = CreateUser();
        user.SetNickname("Fragmaster");

        user.SetNickname(nickname);

        Assert.Null(user.Nickname);
        Assert.Equal("SteamGuy", user.DisplayName);
    }

    [Fact]
    public void SyncSteamName_WithoutNickname_UpdatesDisplayName()
    {
        var user = CreateUser();

        user.SyncSteamName("NewSteamName");

        Assert.Equal("NewSteamName", user.SteamName);
        Assert.Equal("NewSteamName", user.DisplayName);
    }

    [Fact]
    public void SyncSteamName_WithNickname_KeepsNicknameAsDisplayName()
    {
        var user = CreateUser();
        user.SetNickname("Fragmaster");

        user.SyncSteamName("NewSteamName");

        Assert.Equal("NewSteamName", user.SteamName);
        Assert.Equal("Fragmaster", user.Nickname);
        Assert.Equal("Fragmaster", user.DisplayName);
    }

    [Fact]
    public void SetNickname_AfterSteamRename_ClearingUsesLatestSteamName()
    {
        var user = CreateUser();
        user.SetNickname("Fragmaster");
        user.SyncSteamName("NewSteamName");

        user.SetNickname(null);

        Assert.Equal("NewSteamName", user.DisplayName);
    }
}

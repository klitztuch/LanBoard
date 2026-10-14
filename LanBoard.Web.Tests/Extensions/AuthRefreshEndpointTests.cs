using System.Security.Claims;
using LanBoard.Application.Users;
using LanBoard.Core.Entities;
using LanBoard.Web.Auth;
using LanBoard.Web.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LanBoard.Web.Tests.Extensions;

public class AuthRefreshEndpointTests
{
    private const string CookieScheme = CookieAuthenticationDefaults.AuthenticationScheme;

    private readonly IUserService _users = Substitute.For<IUserService>();
    private readonly IAuthenticationService _auth = Substitute.For<IAuthenticationService>();
    private ClaimsPrincipal? _signedIn;
    private AuthenticationProperties? _signInProperties;

    public AuthRefreshEndpointTests()
    {
        _auth.AuthenticateAsync(Arg.Any<HttpContext>(), CookieScheme).Returns(AuthenticateResult.NoResult());
        _auth.SignInAsync(Arg.Any<HttpContext>(), CookieScheme, Arg.Do<ClaimsPrincipal>(p => _signedIn = p), Arg.Do<AuthenticationProperties?>(p => _signInProperties = p))
            .Returns(Task.CompletedTask);
    }

    private DefaultHttpContext SignedIn(Guid userId, string name = "SteamGuy")
    {
        var cookieIdentity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.NameIdentifier, "76561198000000001"),
            new Claim(LanBoardClaims.UserId, userId.ToString()),
            new Claim(LanBoardClaims.AvatarUrl, "old.png"),
        ], CookieScheme);
        var permissionIdentity = new ClaimsIdentity(
            [new Claim(LanBoardClaims.Permission, "party.manage")], PermissionClaimsTransformation.AuthenticationType);

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal([cookieIdentity, permissionIdentity]),
            RequestServices = new ServiceCollection().AddSingleton(_auth).BuildServiceProvider()
        };
    }

    private User GivenUser(Guid id, string? nickname = "Fragmaster", string? avatarUrl = "new.png")
    {
        var user = new User { Id = id, DisplayName = "SteamGuy", SteamName = "SteamGuy", AvatarUrl = avatarUrl };
        user.SetNickname(nickname);
        _users.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    [Fact]
    public async Task ReissuesCookieWithCurrentNameAndAvatar_KeepsOtherClaims()
    {
        var userId = Guid.NewGuid();
        GivenUser(userId);

        await AuthExtensions.RefreshAsync("/settings", SignedIn(userId), _users);

        Assert.NotNull(_signedIn);
        Assert.Equal("Fragmaster", _signedIn.Identity?.Name);
        Assert.Equal("new.png", _signedIn.FindFirstValue(LanBoardClaims.AvatarUrl));
        Assert.Equal(userId, _signedIn.GetUserId());
        Assert.Equal("76561198000000001", _signedIn.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Single(_signedIn.FindAll(ClaimTypes.Name));
    }

    [Fact]
    public async Task PermissionClaims_AreNotWrittenToTheCookie()
    {
        var userId = Guid.NewGuid();
        GivenUser(userId);

        await AuthExtensions.RefreshAsync("/", SignedIn(userId), _users);

        Assert.NotNull(_signedIn);
        Assert.Single(_signedIn.Identities);
        Assert.Empty(_signedIn.FindAll(LanBoardClaims.Permission));
    }

    [Fact]
    public async Task AvatarRemoved_DropsAvatarClaim()
    {
        var userId = Guid.NewGuid();
        GivenUser(userId, avatarUrl: null);

        await AuthExtensions.RefreshAsync("/", SignedIn(userId), _users);

        Assert.Null(_signedIn?.FindFirst(LanBoardClaims.AvatarUrl));
    }

    [Fact]
    public async Task KeepsExistingAuthenticationProperties()
    {
        var userId = Guid.NewGuid();
        GivenUser(userId);
        var ctx = SignedIn(userId);
        var properties = new AuthenticationProperties { IsPersistent = true };
        _auth.AuthenticateAsync(ctx, CookieScheme)
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(ctx.User, properties, CookieScheme)));

        await AuthExtensions.RefreshAsync("/", ctx, _users);

        Assert.Same(properties, _signInProperties);
    }

    [Theory]
    [InlineData("/settings?saved=1", "/settings?saved=1")]
    [InlineData("https://evil.example/", "/")]
    [InlineData("//evil.example/", "/")]
    [InlineData("/\\evil.example/", "/")]
    [InlineData(null, "/")]
    public async Task RedirectsOnlyToLocalUrls(string? returnUrl, string expected)
    {
        var userId = Guid.NewGuid();
        GivenUser(userId);

        var result = await AuthExtensions.RefreshAsync(returnUrl, SignedIn(userId), _users);

        Assert.Equal(expected, Assert.IsType<RedirectHttpResult>(result).Url);
    }

    [Fact]
    public async Task UnknownUser_SignsOut()
    {
        var ctx = SignedIn(Guid.NewGuid());

        var result = await AuthExtensions.RefreshAsync("/settings", ctx, _users);

        await _auth.Received(1).SignOutAsync(ctx, CookieScheme, Arg.Any<AuthenticationProperties?>());
        Assert.Null(_signedIn);
        Assert.Equal("/", Assert.IsType<RedirectHttpResult>(result).Url);
    }
}

using System.Security.Claims;
using LanBoard.Application.Users;
using LanBoard.Web.Auth;
using LanBoard.Web.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LanBoard.Web.Tests.Extensions;

public class CultureEndpointTests
{
    private readonly IUserService _users = Substitute.For<IUserService>();
    private readonly IOptions<LanBoardLocalizationOptions> _options =
        Options.Create(new LanBoardLocalizationOptions { DefaultCulture = "en", SupportedCultures = ["en", "de"] });

    [Fact]
    public async Task UnsupportedCulture_ReturnsBadRequest_AndChangesNothing()
    {
        var ctx = SignedIn(Guid.NewGuid());

        var result = await LocalizationExtensions.SetCultureAsync("fr", "/", ctx, _users, _options);

        Assert.IsType<BadRequest>(result);
        Assert.Equal(0, ctx.Response.Headers.SetCookie.Count);
        await _users.DidNotReceiveWithAnyArgs().SetPreferredCultureAsync(default, default!);
    }

    [Fact]
    public async Task SupportedCulture_SetsCultureCookie()
    {
        var ctx = new DefaultHttpContext();

        await LocalizationExtensions.SetCultureAsync("DE", "/", ctx, _users, _options);

        var cookie = Assert.Single(ctx.Response.Headers.SetCookie.ToArray())!;
        Assert.StartsWith(".AspNetCore.Culture=c%3Dde%7Cuic%3Dde;", cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("samesite=lax", cookie);
    }

    [Fact]
    public async Task Anonymous_DoesNotSavePreference()
    {
        await LocalizationExtensions.SetCultureAsync("de", "/", new DefaultHttpContext(), _users, _options);

        await _users.DidNotReceiveWithAnyArgs().SetPreferredCultureAsync(default, default!);
    }

    [Fact]
    public async Task SignedIn_SavesPreference()
    {
        var userId = Guid.NewGuid();

        await LocalizationExtensions.SetCultureAsync("de", "/", SignedIn(userId), _users, _options);

        await _users.Received(1).SetPreferredCultureAsync(userId, "de", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("/tournaments", "/tournaments")]
    [InlineData("/admin/roles?x=1", "/admin/roles?x=1")]
    [InlineData("https://evil.example/", "/")]
    [InlineData("//evil.example/", "/")]
    [InlineData("/\\evil.example/", "/")]
    [InlineData("tournaments", "/")]
    [InlineData(null, "/")]
    public async Task RedirectsOnlyToLocalUrls(string? returnUrl, string expected)
    {
        var result = await LocalizationExtensions.SetCultureAsync("en", returnUrl, new DefaultHttpContext(), _users, _options);

        Assert.Equal(expected, Assert.IsType<RedirectHttpResult>(result).Url);
    }

    private static DefaultHttpContext SignedIn(Guid userId)
        => new() { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(LanBoardClaims.UserId, userId.ToString())], "Test")) };
}

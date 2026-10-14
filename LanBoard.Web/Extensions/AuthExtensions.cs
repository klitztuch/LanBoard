using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using LanBoard.Application.Authorization;
using LanBoard.Application.Users;
using LanBoard.Infrastructure.ApiClients;
using LanBoard.Infrastructure.ApiClients.Steam;
using LanBoard.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LanBoard.Web.Extensions;

public static class AuthExtensions
{
    public static IHostApplicationBuilder AddAuth(this IHostApplicationBuilder builder)
    {
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = "Steam";
            })
            .AddCookie(options => options.AccessDeniedPath = "/access-denied")
            .AddSteam(options =>
            {
                options.ApplicationKey = builder.Configuration["Steam:ApiKey"];
                options.Events.OnTicketReceived = async ctx =>
                {
                    var nameIdentifier = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    var steamId = nameIdentifier?.Split('/')[^1];
                    var displayName = ctx.Principal?.FindFirstValue(ClaimTypes.Name) ?? "Unknown";

                    if (steamId is null) return;

                    var steamApiClient = ctx.HttpContext.RequestServices
                        .GetRequiredService<ISteamApiClient>();
                    var avatarUrl = await steamApiClient.GetAvatarUrlAsync(steamId);

                    var userService = ctx.HttpContext.RequestServices.GetRequiredService<IUserService>();
                    var user = await userService.GetOrCreateAndSyncSteamProfileAsync(steamId, displayName, avatarUrl);

                    var identity = ctx.Principal?.Identity as ClaimsIdentity;
                    identity?.AddClaim(new Claim(LanBoardClaims.UserId, user.Id.ToString()));
                    if (user.AvatarUrl is not null)
                        identity?.AddClaim(new Claim(LanBoardClaims.AvatarUrl, user.AvatarUrl));
                    // Steam's name claim is the Steam name; the app shows the effective name (nickname wins).
                    if (identity?.FindFirst(ClaimTypes.Name) is { } steamNameClaim)
                        identity.RemoveClaim(steamNameClaim);
                    identity?.AddClaim(new Claim(ClaimTypes.Name, user.DisplayName));
                    ctx.HttpContext.ApplyPreferredCulture(user.PreferredCulture);
                };
            });

        builder.Services.AddScoped<IClaimsTransformation, PermissionClaimsTransformation>();
        builder.Services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions.All)
                options.AddPolicy(permission, p => p.RequireClaim(LanBoardClaims.Permission, permission));
            options.AddPolicy(Policies.AnyAdmin, p => p.RequireClaim(LanBoardClaims.Permission));
        });
        builder.Services.AddCascadingAuthenticationState();

        return builder;
    }

    public static IEndpointRouteBuilder MapAuthRefreshEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/refresh", RefreshAsync).RequireAuthorization();
        return app;
    }

    /// <summary>Re-issues the auth cookie with the user's current name and avatar (e.g. after a nickname change), then redirects back.</summary>
    public static async Task<IResult> RefreshAsync(string? returnUrl, HttpContext ctx, IUserService users)
    {
        var target = LocalUrl.IsLocal(returnUrl) ? returnUrl : "/";
        if (ctx.User.GetUserId() is not { } userId)
            return Results.LocalRedirect(target);

        var user = await users.GetByIdAsync(userId, ctx.RequestAborted);
        if (user is null)
        {
            // Cookie outlived its user (e.g. reset dev DB).
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/");
        }

        // ctx.User includes the per-request permission identity; only the cookie identity is re-issued.
        var cookieIdentity = ctx.User.Identities.First(i => i.AuthenticationType != PermissionClaimsTransformation.AuthenticationType);
        var claims = cookieIdentity.Claims
            .Where(c => c.Type is not (ClaimTypes.Name or LanBoardClaims.AvatarUrl))
            .Select(c => new Claim(c.Type, c.Value, c.ValueType, c.Issuer))
            .Append(new Claim(ClaimTypes.Name, user.DisplayName));
        if (user.AvatarUrl is not null)
            claims = claims.Append(new Claim(LanBoardClaims.AvatarUrl, user.AvatarUrl));

        var identity = new ClaimsIdentity(claims, cookieIdentity.AuthenticationType, ClaimTypes.Name, cookieIdentity.RoleClaimType);
        var existing = await ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), existing.Properties);

        return Results.LocalRedirect(target);
    }
}


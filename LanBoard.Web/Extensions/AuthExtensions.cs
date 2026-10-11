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
                        identity?.AddClaim(new Claim("lanboard:avatarurl", user.AvatarUrl));
                };
            });

        builder.Services.AddScoped<IClaimsTransformation, PermissionClaimsTransformation>();
        builder.Services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions.All.Keys)
                options.AddPolicy(permission, p => p.RequireClaim(LanBoardClaims.Permission, permission));
            options.AddPolicy(Policies.AnyAdmin, p => p.RequireClaim(LanBoardClaims.Permission));
        });
        builder.Services.AddCascadingAuthenticationState();

        return builder;
    }
}


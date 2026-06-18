using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using LanBoard.Application.Users;
using LanBoard.Infrastructure.ApiClients;
using LanBoard.Infrastructure.ApiClients.Steam;
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
            .AddCookie()
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
                    identity?.AddClaim(new Claim("lanboard:userid", user.Id.ToString()));
                    if (user.IsAdmin)
                        identity?.AddClaim(new Claim("lanboard:isadmin", "true"));
                    if (user.AvatarUrl is not null)
                        identity?.AddClaim(new Claim("lanboard:avatarurl", user.AvatarUrl));
                };
            });

        builder.Services.AddAuthorization(options =>
            options.AddPolicy("Admin", p => p.RequireClaim("lanboard:isadmin", "true")));
        builder.Services.AddCascadingAuthenticationState();

        return builder;
    }
}


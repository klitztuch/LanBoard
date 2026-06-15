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
                        .GetRequiredService<SteamApiClient>();
                    var avatarUrl = await steamApiClient.GetAvatarUrlAsync(steamId);

                    var userService = ctx.HttpContext.RequestServices.GetRequiredService<IUserService>();
                    var user = await userService.GetOrCreateAndSyncSteamProfileAsync(steamId, displayName, avatarUrl);

                    (ctx.Principal?.Identity as ClaimsIdentity)?.AddClaim(new Claim("lanboard:userid", user.Id.ToString()));
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();

        return builder;
    }
}


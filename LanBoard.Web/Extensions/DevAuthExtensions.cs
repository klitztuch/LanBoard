using System.Security.Claims;
using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LanBoard.Web.Extensions;

public static class DevAuthExtensions
{
    public static IEndpointRouteBuilder MapDevAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/dev-login", async (string? name, HttpContext ctx, IUserRepository users, IConfiguration config) =>
        {
            if (name is null)
                return Results.BadRequest("name is required");

            var allowed = config.GetSection("DevUsers").Get<string[]>() ?? [];
            if (!allowed.Contains(name))
                return Results.Forbid();

            var user = await users.FindByProviderAsync("Dev", name);
            if (user is null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    DisplayName = name,
                    CreatedAt = DateTime.UtcNow,
                    Identities = [new UserIdentity { Id = Guid.NewGuid(), Provider = "Dev", ProviderUserId = name, CreatedAt = DateTime.UtcNow }]
                };
                await users.AddAsync(user);
                await users.SaveChangesAsync();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, user.DisplayName),
                new(ClaimTypes.NameIdentifier, name),
                new("lanboard:userid", user.Id.ToString())
            };
            if (user.IsAdmin)
                claims.Add(new Claim("lanboard:isadmin", "true"));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            return Results.Redirect("/");
        });

        app.MapGet("/auth/dev-users", (IConfiguration config) =>
        {
            var allowed = config.GetSection("DevUsers").Get<string[]>() ?? [];
            return Results.Ok(allowed.Select(name => new { name, loginUrl = $"/auth/dev-login?name={name}" }));
        });

        return app;
    }
}

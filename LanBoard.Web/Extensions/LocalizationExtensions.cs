using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using LanBoard.Application.Errors;
using LanBoard.Application.Users;
using LanBoard.Web.Auth;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;

namespace LanBoard.Web.Extensions;

public sealed class LanBoardLocalizationOptions
{
    public const string SectionName = "Localization";

    public string DefaultCulture { get; set; } = "en";
    // No default items: the configuration binder appends to an initialized array instead of replacing it.
    public string[] SupportedCultures { get; set; } = [];
}

public static class LocalizationExtensions
{
    public static IHostApplicationBuilder AddLanBoardLocalization(this IHostApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(LanBoardLocalizationOptions.SectionName);
        builder.Services.Configure<LanBoardLocalizationOptions>(section);
        var options = section.Get<LanBoardLocalizationOptions>() ?? new LanBoardLocalizationOptions();

        builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
        builder.Services.Configure<RequestLocalizationOptions>(o =>
        {
            var cultures = options.SupportedCultures.DefaultIfEmpty(options.DefaultCulture).Select(CultureInfo.GetCultureInfo).ToList();
            o.SupportedCultures = cultures;
            o.SupportedUICultures = cultures;
            o.DefaultRequestCulture = new RequestCulture(options.DefaultCulture);
            o.FallBackToParentCultures = true;
            o.FallBackToParentUICultures = true;
            // Explicit choice (cookie) wins over the browser; no query-string override.
            o.RequestCultureProviders =
            [
                new CookieRequestCultureProvider(),
                new AcceptLanguageHeaderRequestCultureProvider()
            ];
        });

        return builder;
    }

    public static WebApplication UseLanBoardLocalization(this WebApplication app)
    {
        app.UseRequestLocalization();
        return app;
    }

    public static IEndpointRouteBuilder MapCultureEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/culture/set", SetCultureAsync);
        return app;
    }

    /// <summary>Stores the chosen culture in the culture cookie (and, when signed in, as the user's preference), then redirects back.</summary>
    public static async Task<IResult> SetCultureAsync(
        string? culture, string? returnUrl, HttpContext ctx, IUserService users, IOptions<LanBoardLocalizationOptions> options)
    {
        if (!options.IsSupportedCulture(culture))
            return Results.BadRequest();

        // Use the configured spelling, not whatever casing the query string carried.
        var supported = options.Value.SupportedCultures.First(c => string.Equals(c, culture, StringComparison.OrdinalIgnoreCase));
        ctx.Response.AppendCultureCookie(supported);

        if (ctx.User.GetUserId() is { } userId)
        {
            try
            {
                await users.SetPreferredCultureAsync(userId, supported, ctx.RequestAborted);
            }
            catch (BusinessRuleException)
            {
                // Cookie outlived its user (e.g. reset dev DB): the cookie choice still applies.
            }
        }

        return Results.LocalRedirect(IsLocalUrl(returnUrl) ? returnUrl : "/");
    }

    // Same rules as IUrlHelper.IsLocalUrl: "/path" or "~/path", but not "//host" or "/\host".
    private static bool IsLocalUrl([NotNullWhen(true)] string? url)
        => !string.IsNullOrEmpty(url) && url switch
        {
            ['/'] => true,
            ['/', not ('/' or '\\'), ..] => true,
            ['~', '/', ..] when url.Length == 2 || url[2] is not ('/' or '\\') => true,
            _ => false
        };

    public static bool IsSupportedCulture(this IOptions<LanBoardLocalizationOptions> options, [NotNullWhen(true)] string? culture)
        => culture is not null && options.Value.SupportedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase);

    /// <summary>Carries a signed-in user's stored language choice over to this device; unsupported (stale) values are ignored.</summary>
    public static void ApplyPreferredCulture(this HttpContext ctx, string? preferredCulture)
    {
        var options = ctx.RequestServices.GetRequiredService<IOptions<LanBoardLocalizationOptions>>();
        if (options.IsSupportedCulture(preferredCulture))
            ctx.Response.AppendCultureCookie(preferredCulture);
    }

    public static void AppendCultureCookie(this HttpResponse response, string culture)
        => response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            });
}

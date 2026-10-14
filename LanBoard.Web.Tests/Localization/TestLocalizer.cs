using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LanBoard.Web.Tests.Localization;

/// <summary>A real resx-backed localizer, configured like the app.</summary>
internal static class TestLocalizer
{
    public static IStringLocalizer<SharedResource> Create()
    {
        var factory = new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }),
            NullLoggerFactory.Instance);
        return new StringLocalizer<SharedResource>(factory);
    }

    /// <summary>Runs <paramref name="action"/> with both current cultures set to <paramref name="culture"/>.</summary>
    public static T InCulture<T>(string culture, Func<T> action)
    {
        var (previous, previousUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try { return action(); }
        finally { (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (previous, previousUi); }
    }
}

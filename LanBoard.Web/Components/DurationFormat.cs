using Microsoft.Extensions.Localization;

namespace LanBoard.Web.Components;

public static class DurationFormat
{
    public static string Format(IStringLocalizer localizer, TimeSpan duration)
        => duration.TotalHours >= 1
            ? localizer["Stats.Duration.HoursMinutes", (int)duration.TotalHours, duration.Minutes]
            : localizer["Stats.Duration.Minutes", duration.Minutes];
}

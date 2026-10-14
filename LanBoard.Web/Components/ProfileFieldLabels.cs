using System.Globalization;
using LanBoard.Application.Profiles;
using LanBoard.Core.Entities;
using Microsoft.Extensions.Localization;

namespace LanBoard.Web.Components;

/// <summary>Resolves a custom profile field's label: current UI culture → its parent → default culture → key.</summary>
public static class ProfileFieldLabels
{
    public static string For(ProfileFieldDefinition definition, string defaultCulture)
        => For(definition.Labels, definition.Key, defaultCulture);

    public static string For(CustomFieldValueView field, string defaultCulture)
        => For(field.Labels, field.Key, defaultCulture);

    public static string For(IReadOnlyDictionary<string, string> labels, string key, string defaultCulture)
    {
        var culture = CultureInfo.CurrentUICulture;
        foreach (var name in new[] { culture.Name, culture.Parent.Name, defaultCulture })
        {
            if (!string.IsNullOrEmpty(name) && labels.TryGetValue(name, out var label) && !string.IsNullOrWhiteSpace(label))
                return label;
        }
        return key;
    }

    /// <summary>Display text of a stored value: YesNo values are localized, everything else is shown as stored.</summary>
    public static string FormatValue(IStringLocalizer localizer, ProfileFieldType type, string value) => (type, value) switch
    {
        (ProfileFieldType.YesNo, "yes") => localizer["Common.Yes"],
        (ProfileFieldType.YesNo, "no") => localizer["Common.No"],
        _ => value
    };
}

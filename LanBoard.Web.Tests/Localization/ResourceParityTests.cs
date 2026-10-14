using System.Text.RegularExpressions;

namespace LanBoard.Web.Tests.Localization;

public partial class ResourceParityTests
{
    public static TheoryData<string> Cultures => new(ResourceFiles.All.Keys.Where(c => c != ResourceFiles.Neutral));

    [Fact]
    public void AtLeastOneTranslationExists()
        => Assert.NotEmpty(Cultures);

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Culture_HasSameKeysAsNeutral(string culture)
    {
        var neutral = ResourceFiles.NeutralStrings.Keys.ToHashSet();
        var translated = ResourceFiles.All[culture].Keys.ToHashSet();

        var missing = neutral.Except(translated).Order().ToList();
        var extra = translated.Except(neutral).Order().ToList();

        Assert.True(missing.Count == 0 && extra.Count == 0,
            $"Culture '{culture}': missing keys [{string.Join(", ", missing)}]; keys not in neutral resx [{string.Join(", ", extra)}]");
    }

    [Fact]
    public void NoEmptyValues()
    {
        var empty = ResourceFiles.All
            .SelectMany(c => c.Value.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => $"{Label(c.Key)}:{kv.Key}"))
            .ToList();

        Assert.True(empty.Count == 0, $"Empty values: {string.Join(", ", empty)}");
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void Culture_HasSamePlaceholdersAsNeutral(string culture)
    {
        var mismatched = ResourceFiles.NeutralStrings
            .Where(kv => ResourceFiles.All[culture].TryGetValue(kv.Key, out var translated)
                         && !Placeholders(kv.Value).SetEquals(Placeholders(translated)))
            .Select(kv => kv.Key)
            .ToList();

        Assert.True(mismatched.Count == 0, $"Culture '{culture}': placeholder mismatch in [{string.Join(", ", mismatched)}]");
    }

    private static HashSet<string> Placeholders(string value)
        => PlaceholderRegex().Matches(value).Select(m => m.Groups[1].Value).ToHashSet();

    private static string Label(string culture) => culture == ResourceFiles.Neutral ? "neutral" : culture;

    [GeneratedRegex(@"\{(\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex PlaceholderRegex();
}

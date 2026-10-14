using System.Text.RegularExpressions;

namespace LanBoard.Web.Tests.Localization;

/// <summary>
/// Best-effort source scan: literal keys used in the Web project must exist in the neutral resx.
/// Dynamic keys (<c>$"Error.{code}"</c>) are covered by <see cref="CatalogCoverageTests"/>.
/// </summary>
public partial class KeyUsageTests
{
    private static readonly string[] ExcludedDirectories = ["bin", "obj", "node_modules", "wwwroot"];

    [Fact]
    public void SourceFilesUseLocalizationKeys()
        => Assert.NotEmpty(UsedKeys());

    [Fact]
    public void EveryLiteralKey_ExistsInNeutralResources()
    {
        var missing = UsedKeys()
            .Where(u => !ResourceFiles.NeutralStrings.ContainsKey(u.Key))
            .Select(u => $"{u.Key} ({u.File})")
            .Distinct()
            .ToList();

        Assert.True(missing.Count == 0, $"Keys missing in SharedResource.resx: {string.Join(", ", missing)}");
    }

    [Fact]
    public void NoHardCodedDateFormats()
    {
        var offenders = SourceFiles()
            .SelectMany(f => HardCodedDateFormatRegex().Matches(File.ReadAllText(f)).Select(m => $"{Relative(f)}: {m.Value}"))
            .ToList();

        Assert.True(offenders.Count == 0, $"Use culture-aware formats (\"d\", \"g\") instead: {string.Join(", ", offenders)}");
    }

    private static List<(string Key, string File)> UsedKeys()
        => SourceFiles()
            .SelectMany(f => LiteralKeyRegex().Matches(File.ReadAllText(f)).Select(m => (m.Groups[1].Value, Relative(f))))
            .ToList();

    private static IEnumerable<string> SourceFiles()
        => new[] { "*.razor", "*.cs" }
            .SelectMany(pattern => Directory.EnumerateFiles(ResourceFiles.WebProjectDirectory, pattern, SearchOption.AllDirectories))
            .Where(f => !Relative(f).Split(Path.DirectorySeparatorChar).Intersect(ExcludedDirectories).Any());

    private static string Relative(string path) => Path.GetRelativePath(ResourceFiles.WebProjectDirectory, path);

    // L["Key"], L["Key", args], localizer["Key"], Localizer["Key"]; not interpolated $"…" keys.
    [GeneratedRegex(@"(?<![\w.])(?:L|[Ll]ocalizer)\[""([^""]+)""")]
    private static partial Regex LiteralKeyRegex();

    [GeneratedRegex(@"ToString\(""[^""]*(?:dd|MM|yyyy|HH)[^""]*""\)")]
    private static partial Regex HardCodedDateFormatRegex();
}

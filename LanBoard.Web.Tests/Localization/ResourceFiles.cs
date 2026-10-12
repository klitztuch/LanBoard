using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace LanBoard.Web.Tests.Localization;

/// <summary>Reads the shared .resx files straight from the Web project's source directory.</summary>
internal static class ResourceFiles
{
    public const string Neutral = "";

    public static string WebProjectDirectory { get; } = FindWebProjectDirectory();

    private static string ResourcesDirectory => Path.Combine(WebProjectDirectory, "Resources");

    /// <summary>Culture suffix ("" for the neutral file, "de", …) → key/value pairs.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> All { get; } = LoadAll();

    public static IReadOnlyDictionary<string, string> NeutralStrings => All[Neutral];

    private static Dictionary<string, IReadOnlyDictionary<string, string>> LoadAll()
        => Directory.GetFiles(ResourcesDirectory, "SharedResource*.resx")
            .ToDictionary(CultureOf, Load);

    private static string CultureOf(string path)
    {
        var parts = Path.GetFileNameWithoutExtension(path).Split('.', 2);
        return parts.Length == 2 ? parts[1] : Neutral;
    }

    private static IReadOnlyDictionary<string, string> Load(string path)
        => XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    private static string FindWebProjectDirectory([CallerFilePath] string thisFile = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "LanBoard.Web")))
            dir = dir.Parent;
        return dir is null
            ? throw new DirectoryNotFoundException("LanBoard.Web project directory not found.")
            : Path.Combine(dir.FullName, "LanBoard.Web");
    }
}

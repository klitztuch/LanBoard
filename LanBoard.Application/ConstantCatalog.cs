using System.Reflection;

namespace LanBoard.Application;

internal static class ConstantCatalog
{
    // All public const string values of a catalog class, so tests can check resources against them.
    public static IReadOnlyList<string> Of(Type type)
        => type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();
}

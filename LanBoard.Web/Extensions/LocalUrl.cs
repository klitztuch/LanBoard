using System.Diagnostics.CodeAnalysis;

namespace LanBoard.Web.Extensions;

public static class LocalUrl
{
    // Same rules as IUrlHelper.IsLocalUrl: "/path" or "~/path", but not "//host" or "/\host".
    public static bool IsLocal([NotNullWhen(true)] string? url)
        => !string.IsNullOrEmpty(url) && url switch
        {
            ['/'] => true,
            ['/', not ('/' or '\\'), ..] => true,
            ['~', '/', ..] when url.Length == 2 || url[2] is not ('/' or '\\') => true,
            _ => false
        };
}

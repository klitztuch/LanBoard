using System.Globalization;
using LanBoard.Core.Entities;

namespace LanBoard.Web.Components;

public static class MapsLinks
{
    /// <summary>Google Maps directions to the location: coordinates if set, otherwise the address; null if neither.</summary>
    public static string? Directions(Location location)
    {
        var destination = Destination(location);
        return string.IsNullOrEmpty(destination)
            ? null
            : $"https://www.google.com/maps/dir/?api=1&destination={Uri.EscapeDataString(destination)}";
    }

    private static string? Destination(Location location)
    {
        if (location is { Latitude: { } lat, Longitude: { } lng })
            return string.Create(CultureInfo.InvariantCulture, $"{lat},{lng}");
        if (location.Address is { } address)
            return string.Join(", ", address.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return null;
    }
}

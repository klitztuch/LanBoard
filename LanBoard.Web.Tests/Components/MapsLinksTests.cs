using System.Globalization;
using LanBoard.Core.Entities;
using LanBoard.Web.Components;

namespace LanBoard.Web.Tests.Components;

public class MapsLinksTests
{
    [Fact]
    public void Coordinates_ArePreferredAndFormattedInvariantly()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var location = new Location { Name = "Garage", Address = "Main St 1", Latitude = 52.52, Longitude = -13.405 };

            Assert.Equal("https://www.google.com/maps/dir/?api=1&destination=52.52%2C-13.405", MapsLinks.Directions(location));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void WithoutCoordinates_UsesEscapedAddressWithLinesJoined()
    {
        var location = new Location { Name = "Garage", Address = "Main St 1\r\n12345 Town & Co\n" };

        Assert.Equal("https://www.google.com/maps/dir/?api=1&destination=Main%20St%201%2C%2012345%20Town%20%26%20Co",
            MapsLinks.Directions(location));
    }

    [Fact]
    public void WithoutCoordinatesOrAddress_IsNull()
        => Assert.Null(MapsLinks.Directions(new Location { Name = "Garage" }));
}

using Bunit;
using LanBoard.Application.Seats;
using LanBoard.Core.Entities;
using LanBoard.Web.Components.Pages;
using LanBoard.Web.Tests.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace LanBoard.Web.Tests.Components;

public class PartyInfoTests : BunitContext
{
    private readonly ISeatService _seats = Substitute.For<ISeatService>();

    public PartyInfoTests()
    {
        TestLocalizer.UseCulture("en");
        Services.AddSingleton(_seats);
        Services.AddSingleton<IStringLocalizer<SharedResource>>(TestLocalizer.Create());
    }

    [Fact]
    public void NoActiveParty_ShowsHint()
    {
        _seats.GetActivePartyWithSeatsAsync(null, Arg.Any<CancellationToken>()).Returns((LanParty?)null);

        var cut = Render<PartyInfo>();

        Assert.Contains("No active LAN party found.", cut.Markup);
    }

    [Fact]
    public void EmptyInfoBoard_ShowsPartyNameHint()
    {
        _seats.GetActivePartyWithSeatsAsync(null, Arg.Any<CancellationToken>()).Returns(Party(infoBoard: null));

        var cut = Render<PartyInfo>();

        Assert.Contains("No info has been added for LAN #1 yet.", cut.Markup);
    }

    [Fact]
    public void InfoBoard_RendersTextWithLineBreaksPreserved()
    {
        _seats.GetActivePartyWithSeatsAsync(null, Arg.Any<CancellationToken>()).Returns(Party("WLAN: lan2026\nStart: 18:00"));

        var cut = Render<PartyInfo>();

        var board = cut.Find("div.whitespace-pre-wrap");
        Assert.Equal("WLAN: lan2026\nStart: 18:00", board.TextContent);
    }

    [Fact]
    public void InfoBoard_HtmlIsEncodedNotRendered()
    {
        _seats.GetActivePartyWithSeatsAsync(null, Arg.Any<CancellationToken>()).Returns(Party("<script>alert(1)</script><b>bold</b>"));

        var cut = Render<PartyInfo>();

        var board = cut.Find("div.whitespace-pre-wrap");
        Assert.Empty(board.Children);
        Assert.Equal("<script>alert(1)</script><b>bold</b>", board.TextContent);
    }

    [Fact]
    public void PartyIdQuery_IsPassedToService()
    {
        var partyId = Guid.NewGuid();
        _seats.GetActivePartyWithSeatsAsync(partyId, Arg.Any<CancellationToken>()).Returns(Party("Specific party"));
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/info?partyId={partyId}");

        var cut = Render<PartyInfo>();

        Assert.Contains("Specific party", cut.Find("div.whitespace-pre-wrap").TextContent);
    }

    [Fact]
    public void Location_ShowsNameAddressAndDetails()
    {
        var location = new Location
        {
            Name = "Garage", Address = "Main St 1\n12345 Town", Directions = "Second exit", Parking = "Backyard", Notes = "Bring a cable"
        };
        _seats.GetActivePartyWithSeatsAsync(null, Arg.Any<CancellationToken>()).Returns(Party(null, location));

        var cut = Render<PartyInfo>();

        var section = cut.Find("section#location");
        Assert.Equal("Garage", section.QuerySelector("h2 + p")!.TextContent);
        Assert.Contains("Main St 1\n12345 Town", section.TextContent);
        Assert.Contains("Second exit", section.TextContent);
        Assert.Contains("Backyard", section.TextContent);
        Assert.Contains("Bring a cable", section.TextContent);
    }

    [Fact]
    public void Location_EmptyDetailsAreOmitted()
    {
        _seats.GetActivePartyWithSeatsAsync(null, Arg.Any<CancellationToken>()).Returns(Party(null, new Location { Name = "Garage" }));

        var cut = Render<PartyInfo>();

        var section = cut.Find("section#location");
        Assert.Empty(section.QuerySelectorAll("h3"));
        Assert.Empty(section.QuerySelectorAll("a"));
    }

    [Fact]
    public void Location_DirectionsLinkUsesCoordinatesAndOpensInNewTab()
    {
        var location = new Location { Name = "Garage", Address = "Main St 1", Latitude = 52.52, Longitude = 13.405 };
        _seats.GetActivePartyWithSeatsAsync(null, Arg.Any<CancellationToken>()).Returns(Party(null, location));

        var cut = Render<PartyInfo>();

        var link = cut.Find("section#location a");
        Assert.Equal("https://www.google.com/maps/dir/?api=1&destination=52.52%2C13.405", link.GetAttribute("href"));
        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Contains("noopener", link.GetAttribute("rel"));
    }

    [Fact]
    public void Location_IsShownEvenWithoutInfoBoard()
    {
        _seats.GetActivePartyWithSeatsAsync(null, Arg.Any<CancellationToken>()).Returns(Party(null));

        var cut = Render<PartyInfo>();

        Assert.Contains("No info has been added for LAN #1 yet.", cut.Markup);
        Assert.Contains("Garage", cut.Find("section#location").TextContent);
    }

    private static LanParty Party(string? infoBoard, Location? location = null)
        => new() { Id = Guid.NewGuid(), Name = "LAN #1", Location = location ?? new Location { Name = "Garage" }, InfoBoard = infoBoard, CreatedByUserId = Guid.NewGuid() };
}

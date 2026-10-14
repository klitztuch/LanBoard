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

    private static LanParty Party(string? infoBoard)
        => new() { Id = Guid.NewGuid(), Name = "LAN #1", Location = "Garage", InfoBoard = infoBoard, CreatedByUserId = Guid.NewGuid() };
}

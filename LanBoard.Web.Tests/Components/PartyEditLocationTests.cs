using System.Security.Claims;
using Bunit;
using LanBoard.Application.Admin;
using LanBoard.Application.Locations;
using LanBoard.Core.Entities;
using LanBoard.Web.Auth;
using LanBoard.Web.Components.Pages.Admin;
using LanBoard.Web.Tests.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace LanBoard.Web.Tests.Components;

public class PartyEditLocationTests : BunitContext
{
    private readonly IAdminService _admin = Substitute.For<IAdminService>();
    private readonly ILocationService _locations = Substitute.For<ILocationService>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Location _garage = new() { Id = Guid.NewGuid(), Name = "Garage" };
    private readonly Location _basement = new() { Id = Guid.NewGuid(), Name = "Basement" };

    public PartyEditLocationTests()
    {
        TestLocalizer.UseCulture("en");
        Services.AddSingleton(_admin);
        Services.AddSingleton(_locations);
        Services.AddSingleton<IStringLocalizer<SharedResource>>(TestLocalizer.Create());
        AddAuthorization().SetAuthorized("Alice").SetClaims(new Claim(LanBoardClaims.UserId, _userId.ToString()));
        _locations.GetAllAsync(Arg.Any<CancellationToken>()).Returns([_basement, _garage]);
    }

    [Fact]
    public void ExistingParty_PreselectsItsLocation()
    {
        var party = Party("LAN #2", _garage, 2026, seats: 0);

        var cut = RenderExisting(party, []);

        Assert.Equal(_garage.Id.ToString(), cut.Find("select#party-location").GetAttribute("value"));
    }

    [Fact]
    public void NewLocationModal_SelectsCreatedLocationAndKeepsPartyInput()
    {
        var created = new Location { Id = Guid.NewGuid(), Name = "Attic" };
        _locations.CreateAsync(Arg.Is<LocationInput>(i => i.Name == "Attic"), _userId, Arg.Any<CancellationToken>()).Returns(created);
        var cut = Render<PartyEdit>();
        cut.Find("input").Change("LAN #3");

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "New location").Click();
        _locations.GetAllAsync(Arg.Any<CancellationToken>()).Returns([created, _basement, _garage]);
        var modal = cut.Find("[role=dialog]");
        modal.QuerySelector("input")!.Change("Attic");
        cut.Find("[role=dialog] form").Submit();

        Assert.Empty(cut.FindAll("[role=dialog]"));
        Assert.Equal(created.Id.ToString(), cut.Find("select#party-location").GetAttribute("value"));
        Assert.Equal("LAN #3", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void EmptySeatPlan_PreselectsMostRecentPartyAtSameLocation()
    {
        var target = Party("LAN #3", _garage, 2026, seats: 0);
        var older = Party("LAN #1", _garage, 2024);
        var newest = Party("LAN #2", _garage, 2025);
        var elsewhere = Party("Basement LAN", _basement, 2025);

        var cut = RenderExisting(target, [target, older, newest, elsewhere]);

        Assert.Equal(newest.Id.ToString(), cut.Find("select#seat-plan-source").GetAttribute("value"));
    }

    [Fact]
    public void CopySeatPlan_CallsServiceAndShowsCopiedSeats()
    {
        var target = Party("LAN #3", _garage, 2026, seats: 0);
        var source = Party("LAN #2", _garage, 2025);
        var cut = RenderExisting(target, [target, source]);
        var copied = Party("LAN #3", _garage, 2026);
        copied.Id = target.Id;
        _admin.GetPartyWithSeatsAsync(target.Id, Arg.Any<CancellationToken>()).Returns(copied);

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Copy").Click();

        _admin.Received(1).CopySeatsAsync(target.Id, source.Id, _userId, Arg.Any<CancellationToken>());
        Assert.Empty(cut.FindAll("select#seat-plan-source"));
    }

    [Fact]
    public void ExistingSeats_HideSeatPlanCopy()
    {
        var target = Party("LAN #3", _garage, 2026);

        var cut = RenderExisting(target, [target, Party("LAN #2", _garage, 2025)]);

        Assert.Empty(cut.FindAll("select#seat-plan-source"));
    }

    private IRenderedComponent<PartyEdit> RenderExisting(LanParty party, IReadOnlyList<LanParty> all)
    {
        _admin.GetPartyWithSeatsAsync(party.Id, Arg.Any<CancellationToken>()).Returns(party);
        _admin.GetAllPartiesAsync(Arg.Any<CancellationToken>()).Returns(all);
        return Render<PartyEdit>(p => p.Add(c => c.Id, party.Id));
    }

    private static LanParty Party(string name, Location location, int year, int seats = 2) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Date = new DateTime(year, 1, 1),
        LocationId = location.Id,
        Location = location,
        CreatedByUserId = Guid.NewGuid(),
        Seats = [.. Enumerable.Range(0, seats).Select(i => new Seat { Id = Guid.NewGuid(), Label = $"{i}1", X = i, Y = 1 })]
    };
}

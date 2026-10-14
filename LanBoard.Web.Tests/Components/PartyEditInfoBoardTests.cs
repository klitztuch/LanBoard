using Bunit;
using LanBoard.Application.Admin;
using LanBoard.Application.Errors;
using LanBoard.Core.Entities;
using LanBoard.Web.Auth;
using LanBoard.Web.Components.Pages.Admin;
using LanBoard.Web.Tests.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Security.Claims;

namespace LanBoard.Web.Tests.Components;

public class PartyEditInfoBoardTests : BunitContext
{
    private readonly IAdminService _admin = Substitute.For<IAdminService>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly LanParty _party = new()
    {
        Id = Guid.NewGuid(), Name = "LAN #1", Location = "Garage", InfoBoard = "WLAN: lan2026", CreatedByUserId = Guid.NewGuid()
    };

    public PartyEditInfoBoardTests()
    {
        TestLocalizer.UseCulture("en");
        Services.AddSingleton(_admin);
        Services.AddSingleton<IStringLocalizer<SharedResource>>(TestLocalizer.Create());
        AddAuthorization().SetAuthorized("Alice").SetClaims(new Claim(LanBoardClaims.UserId, _userId.ToString()));
        _admin.GetPartyWithSeatsAsync(_party.Id, Arg.Any<CancellationToken>()).Returns(_party);
    }

    [Fact]
    public void NewParty_HasNoInfoBoardSection()
    {
        var cut = Render<PartyEdit>();

        Assert.Empty(cut.FindAll("textarea"));
        Assert.DoesNotContain("Save info board", cut.Markup);
    }

    [Fact]
    public void ExistingParty_PrefillsInfoBoardWithMaxLength()
    {
        var cut = RenderExisting();

        var textarea = cut.Find("textarea");
        Assert.Equal("WLAN: lan2026", textarea.GetAttribute("value") ?? textarea.TextContent);
        Assert.Equal("4000", textarea.GetAttribute("maxlength"));
        Assert.Contains("Info board", cut.Markup);
    }

    [Fact]
    public void Save_CallsServiceWithTextAndCurrentUser()
    {
        var cut = RenderExisting();

        cut.Find("textarea").Change("WLAN: neu\nStart 18:00");
        InfoBoardForm(cut).Submit();

        _admin.Received(1).UpdateInfoBoardAsync(_party.Id, "WLAN: neu\nStart 18:00", _userId, Arg.Any<CancellationToken>());
        Assert.Empty(cut.FindAll("p.bg-red-950"));
    }

    [Fact]
    public void Save_BusinessRuleViolation_ShowsLocalizedError()
    {
        _admin.UpdateInfoBoardAsync(_party.Id, Arg.Any<string?>(), _userId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BusinessRuleException(ErrorCodes.PartyInfoBoardTooLong, 4000));
        var cut = RenderExisting();

        InfoBoardForm(cut).Submit();

        Assert.Equal("The info board may contain at most 4000 characters.", cut.Find("p.bg-red-950").TextContent);
    }

    [Fact]
    public void Save_PermissionDenied_ShowsErrorInsteadOfCrashing()
    {
        _admin.UpdateInfoBoardAsync(_party.Id, Arg.Any<string?>(), _userId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new LanBoard.Application.Authorization.PermissionDeniedException(LanBoard.Application.Authorization.Permissions.PartyManage));
        var cut = RenderExisting();

        InfoBoardForm(cut).Submit();

        Assert.NotEmpty(cut.Find("p.bg-red-950").TextContent);
    }

    private IRenderedComponent<PartyEdit> RenderExisting()
        => Render<PartyEdit>(p => p.Add(c => c.Id, _party.Id));

    // The page has two forms (party details, info board); the info board form is the one with the textarea.
    private static AngleSharp.Dom.IElement InfoBoardForm(IRenderedComponent<PartyEdit> cut)
        => cut.FindAll("form").Single(f => f.QuerySelector("textarea") is not null);
}

using LanBoard.Application.Authorization;
using LanBoard.Application.Errors;
using LanBoard.Web.Components;
using LanBoard.Web.Tests.Localization;

namespace LanBoard.Web.Tests.Components;

public class ErrorMessagesTests
{
    private readonly Microsoft.Extensions.Localization.IStringLocalizer<SharedResource> _localizer = TestLocalizer.Create();

    [Theory]
    [InlineData("en", "Party not found.")]
    [InlineData("de", "Party nicht gefunden.")]
    [InlineData("de-AT", "Party nicht gefunden.")]
    [InlineData("fr", "Party not found.")]
    public void BusinessRule_IsLocalized(string culture, string expected)
        => Assert.Equal(expected, TestLocalizer.InCulture(culture,
            () => ErrorMessages.For(_localizer, new BusinessRuleException(ErrorCodes.PartyNotFound))));

    [Fact]
    public void BusinessRule_InsertsArguments()
        => Assert.Equal("Name muss 1–50 Zeichen lang sein.", TestLocalizer.InCulture("de",
            () => ErrorMessages.For(_localizer, new BusinessRuleException(ErrorCodes.RoleNameInvalid, 50))));

    [Fact]
    public void UnknownCode_FallsBackToGenericMessage()
        => Assert.Equal("Something went wrong. Please try again.", TestLocalizer.InCulture("en",
            () => ErrorMessages.For(_localizer, new BusinessRuleException("Nope.Unknown"))));

    [Fact]
    public void PermissionDenied_IsLocalized()
        => Assert.Equal("Keine Berechtigung.", TestLocalizer.InCulture("de",
            () => ErrorMessages.For(_localizer, new PermissionDeniedException(Permissions.PartyManage))));

    [Fact]
    public void OtherException_ReturnsNull()
        => Assert.Null(ErrorMessages.For(_localizer, new InvalidOperationException("boom")));
}

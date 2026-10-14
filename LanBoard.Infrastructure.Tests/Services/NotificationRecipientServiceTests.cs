using LanBoard.Application.Errors;
using LanBoard.Application.Interfaces;
using LanBoard.Application.Notifications;
using LanBoard.Infrastructure.Services;
using NSubstitute;

namespace LanBoard.Infrastructure.Tests.Services;

public class NotificationRecipientServiceTests
{
    private readonly IUserProfileRepository _profiles = Substitute.For<IUserProfileRepository>();
    private readonly NotificationRecipientService _sut;

    public NotificationRecipientServiceTests()
    {
        _sut = new NotificationRecipientService(_profiles);
    }

    [Fact]
    public async Task GetEmailRecipientsAsync_KnownCategory_DelegatesToRepository()
    {
        IReadOnlyList<NotificationRecipient> expected = [new(Guid.NewGuid(), "Bob", "bob@example.com", "de")];
        _profiles.GetEmailRecipientsAsync(NotificationCategories.PartyAnnouncements, Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _sut.GetEmailRecipientsAsync(NotificationCategories.PartyAnnouncements);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetEmailRecipientsAsync_UnknownCategory_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.GetEmailRecipientsAsync("spam"));

        Assert.Equal(ErrorCodes.NotificationUnknownCategory, ex.Code);
        await _profiles.DidNotReceive().GetEmailRecipientsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

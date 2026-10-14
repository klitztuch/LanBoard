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

    [Fact]
    public async Task GetEmailRecipientsAsync_InAppOnlyCategory_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.GetEmailRecipientsAsync(NotificationCategories.AttendeeOnline));

        Assert.Equal(ErrorCodes.NotificationUnknownCategory, ex.Code);
    }

    [Fact]
    public async Task GetInAppSettingsAsync_WithoutProfileRow_ReturnsDefault()
    {
        _profiles.GetInAppSettingsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((InAppNotificationSettings?)null);

        var settings = await _sut.GetInAppSettingsAsync(Guid.NewGuid());

        Assert.Same(InAppNotificationSettings.Default, settings);
        Assert.True(settings.SoundEnabled);
        Assert.True(settings.IsEnabled(NotificationCategories.AttendeeOnline));
    }

    [Fact]
    public async Task GetInAppSettingsAsync_WithProfileRow_ReturnsStoredValues()
    {
        var userId = Guid.NewGuid();
        var stored = new InAppNotificationSettings(new HashSet<string> { NotificationCategories.AttendeeOnline }, false);
        _profiles.GetInAppSettingsAsync(userId, Arg.Any<CancellationToken>()).Returns(stored);

        var settings = await _sut.GetInAppSettingsAsync(userId);

        Assert.False(settings.SoundEnabled);
        Assert.False(settings.IsEnabled(NotificationCategories.AttendeeOnline));
    }
}

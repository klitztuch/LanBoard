using LanBoard.Application.Notifications;
using LanBoard.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LanBoard.Web.Tests.Notifications;

public class InAppNotificationHubTests
{
    private readonly InAppNotificationHub _sut = new(NullLogger<InAppNotificationHub>.Instance);

    [Fact]
    public void Publish_ThrowingSubscriber_DoesNotBlockOthers()
    {
        var received = new List<NotificationMessage>();
        _sut.Published += _ => throw new InvalidOperationException("broken circuit");
        _sut.Published += received.Add;
        var message = new NotificationMessage(NotificationCategories.AttendeeOnline, Guid.NewGuid(), ["Alice"]);

        _sut.Publish(message);

        Assert.Equal([message], received);
    }

    [Fact]
    public void NotifyPreferencesChanged_ThrowingSubscriber_DoesNotBlockOthers()
    {
        var received = new List<Guid>();
        _sut.PreferencesChanged += _ => throw new ObjectDisposedException("host");
        _sut.PreferencesChanged += received.Add;
        var userId = Guid.NewGuid();

        _sut.NotifyPreferencesChanged(userId);

        Assert.Equal([userId], received);
    }

    [Fact]
    public void Publish_WithoutSubscribers_DoesNothing()
        => _sut.Publish(new NotificationMessage(NotificationCategories.AttendeeOnline, null, []));
}

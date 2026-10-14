using LanBoard.Application.Notifications;
using LanBoard.Web.Notifications;

namespace LanBoard.Web.Tests.Notifications;

public class InAppNotificationFilterTests
{
    private readonly Guid _recipient = Guid.NewGuid();

    private static NotificationMessage Message(string category, Guid? actor) => new(category, actor, ["Alice"]);

    [Fact]
    public void OwnAction_NotShown()
        => Assert.False(InAppNotificationFilter.ShouldShow(
            Message(NotificationCategories.AttendeeOnline, _recipient), _recipient, InAppNotificationSettings.Default));

    [Fact]
    public void OptedOutCategory_NotShown()
    {
        var settings = new InAppNotificationSettings(new HashSet<string> { NotificationCategories.AttendeeOnline }, true);

        Assert.False(InAppNotificationFilter.ShouldShow(
            Message(NotificationCategories.AttendeeOnline, Guid.NewGuid()), _recipient, settings));
    }

    [Fact]
    public void CategoryWithoutInAppChannel_NotShown()
        => Assert.False(InAppNotificationFilter.ShouldShow(
            Message(NotificationCategories.PartyAnnouncements, Guid.NewGuid()), _recipient, InAppNotificationSettings.Default));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OtherActorOrNoActor_Shown(bool withActor)
        => Assert.True(InAppNotificationFilter.ShouldShow(
            Message(NotificationCategories.AttendeeOnline, withActor ? Guid.NewGuid() : null), _recipient, InAppNotificationSettings.Default));
}

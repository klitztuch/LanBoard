using LanBoard.Application.Notifications;

namespace LanBoard.Web.Tests.Notifications;

public class NotificationCategoriesTests
{
    [Fact]
    public void EveryCategory_HasChannels()
    {
        var unmapped = NotificationCategories.All
            .Where(c => NotificationCategories.ChannelsOf(c) == NotificationChannels.None)
            .ToList();
        Assert.True(unmapped.Count == 0, $"Categories without channels: {string.Join(", ", unmapped)}");
    }

    [Fact]
    public void AttendeeOnline_IsInAppOnly()
    {
        Assert.Contains(NotificationCategories.AttendeeOnline, NotificationCategories.Supporting(NotificationChannels.InApp));
        Assert.DoesNotContain(NotificationCategories.AttendeeOnline, NotificationCategories.Supporting(NotificationChannels.Email));
    }

    [Fact]
    public void UnknownCategory_HasNoChannels()
        => Assert.Equal(NotificationChannels.None, NotificationCategories.ChannelsOf("does.not.exist"));
}

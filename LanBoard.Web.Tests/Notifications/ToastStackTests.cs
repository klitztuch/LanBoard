using LanBoard.Web.Notifications;

namespace LanBoard.Web.Tests.Notifications;

public class ToastStackTests
{
    private readonly ToastStack _sut = new(capacity: 5);

    [Fact]
    public void Add_BeyondCapacity_DropsOldest()
    {
        for (var i = 1; i <= 6; i++)
            _sut.Add($"t{i}");

        Assert.Equal(["t2", "t3", "t4", "t5", "t6"], _sut.Items.Select(t => t.Text));
    }

    [Fact]
    public void Remove_ById()
    {
        var first = _sut.Add("a");
        _sut.Add("b");

        Assert.True(_sut.Remove(first.Id));
        Assert.False(_sut.Remove(first.Id));
        Assert.Equal(["b"], _sut.Items.Select(t => t.Text));
    }
}

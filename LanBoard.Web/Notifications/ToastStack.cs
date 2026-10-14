namespace LanBoard.Web.Notifications;

public sealed record Toast(Guid Id, string Text);

/// <summary>Visible toasts, newest last; adding beyond the capacity drops the oldest.</summary>
public sealed class ToastStack(int capacity = 5)
{
    private readonly List<Toast> _items = [];

    public IReadOnlyList<Toast> Items => _items;

    public Toast Add(string text)
    {
        var toast = new Toast(Guid.NewGuid(), text);
        _items.Add(toast);
        if (_items.Count > capacity)
            _items.RemoveRange(0, _items.Count - capacity);
        return toast;
    }

    public bool Remove(Guid toastId) => _items.RemoveAll(t => t.Id == toastId) > 0;
}

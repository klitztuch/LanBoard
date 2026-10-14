namespace LanBoard.Application.Errors;

// Message is culture-independent on purpose: it ends up in logs, the UI localizes Code.
public sealed class BusinessRuleException(string code, params object[] args)
    : Exception($"Business rule violated: {code}")
{
    public string Code { get; } = code;
    public IReadOnlyList<object> Args { get; } = args;
}

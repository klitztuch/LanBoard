namespace LanBoard.Application.Errors;

// Field = input property name or "custom:{fieldId}"; Code is an ErrorCodes value, localized by the UI.
public sealed record ValidationError(string Field, string Code, params object[] Args);

// Carries all field errors of one submit at once (BusinessRuleException carries a single code).
public sealed class ValidationException(IReadOnlyList<ValidationError> errors)
    : Exception($"Validation failed: {string.Join(", ", errors.Select(e => $"{e.Field}={e.Code}"))}")
{
    public IReadOnlyList<ValidationError> Errors { get; } = errors;
}

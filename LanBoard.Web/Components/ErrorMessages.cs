using LanBoard.Application.Authorization;
using LanBoard.Application.Errors;
using Microsoft.Extensions.Localization;

namespace LanBoard.Web.Components;

/// <summary>Maps user-facing service exceptions to localized messages.</summary>
public static class ErrorMessages
{
    /// <summary>Localized message for a permission or business-rule failure; null for any other exception.</summary>
    public static string? For(IStringLocalizer localizer, Exception exception) => exception switch
    {
        PermissionDeniedException => localizer["Error.PermissionDenied"],
        BusinessRuleException ex => ForCode(localizer, ex.Code, ex.Args),
        ValidationException => localizer["Error.Validation"],
        _ => null
    };

    /// <summary>Localized message for a single field error.</summary>
    public static string For(IStringLocalizer localizer, ValidationError error)
        => ForCode(localizer, error.Code, error.Args);

    private static string ForCode(IStringLocalizer localizer, string code, IReadOnlyList<object> args)
    {
        var message = localizer[$"Error.{code}", [.. args]];
        return message.ResourceNotFound ? localizer["Error.Generic"] : message;
    }
}

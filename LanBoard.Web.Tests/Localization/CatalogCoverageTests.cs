using LanBoard.Application.Admin;
using LanBoard.Application.Authorization;
using LanBoard.Application.Errors;
using LanBoard.Application.Notifications;
using LanBoard.Core.Entities;

namespace LanBoard.Web.Tests.Localization;

public class CatalogCoverageTests
{
    [Fact]
    public void EveryErrorCode_HasMessage()
        => AssertAllPresent(ErrorCodes.All.Select(c => $"Error.{c}").Append("Error.Generic").Append("Error.PermissionDenied"));

    [Fact]
    public void EveryPermission_HasDisplayName()
        => AssertAllPresent(Permissions.All.Select(p => $"Permission.{p}"));

    [Fact]
    public void EveryAuditAction_HasDisplayName()
        => AssertAllPresent(AuditActions.All.Select(a => $"Audit.{a}"));

    [Fact]
    public void EveryNotificationCategory_HasDisplayName()
        => AssertAllPresent(NotificationCategories.All.Select(c => $"NotificationCategory.{c}"));

    [Fact]
    public void EveryFieldVisibility_HasDisplayName()
        => AssertAllPresent(Enum.GetNames<FieldVisibility>().Select(v => $"Visibility.{v}"));

    [Fact]
    public void EveryProfileFieldType_HasDisplayName()
        => AssertAllPresent(Enum.GetNames<ProfileFieldType>().Select(t => $"ProfileFieldType.{t}"));

    private static void AssertAllPresent(IEnumerable<string> keys)
    {
        var missing = keys.Where(k => !ResourceFiles.NeutralStrings.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, $"Missing resource keys: {string.Join(", ", missing)}");
    }
}

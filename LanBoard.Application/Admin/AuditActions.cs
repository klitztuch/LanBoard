namespace LanBoard.Application.Admin;

public static class AuditActions
{
    public const string PartyCreated = "PartyCreated";
    public const string PartyUpdated = "PartyUpdated";
    public const string PartyDeleted = "PartyDeleted";
    public const string PartyActivated = "PartyActivated";
    public const string PartyInfoBoardUpdated = "PartyInfoBoardUpdated";
    public const string PartySeatsCopied = "PartySeatsCopied";
    public const string LocationCreated = "LocationCreated";
    public const string LocationUpdated = "LocationUpdated";
    public const string LocationDeleted = "LocationDeleted";
    public const string RoleCreated = "RoleCreated";
    public const string RoleUpdated = "RoleUpdated";
    public const string RoleDeleted = "RoleDeleted";
    public const string RoleAssigned = "RoleAssigned";
    public const string RoleUnassigned = "RoleUnassigned";
    public const string ProfileFieldCreated = "ProfileFieldCreated";
    public const string ProfileFieldUpdated = "ProfileFieldUpdated";
    public const string ProfileFieldDeleted = "ProfileFieldDeleted";

    public static IReadOnlyList<string> All { get; } = ConstantCatalog.Of(typeof(AuditActions));
}

namespace LanBoard.Application.Authorization;

public static class Permissions
{
    public const string PartyManage = "party.manage";
    public const string TournamentManage = "tournament.manage";
    public const string CateringManage = "catering.manage";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
    public const string AuditView = "audit.view";

    // Key → German display label for the role editor
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        [PartyManage] = "LAN-Parties verwalten",
        [TournamentManage] = "Turniere verwalten",
        [CateringManage] = "Catering verwalten",
        [UsersManage] = "Benutzer & Rollenzuweisung verwalten",
        [RolesManage] = "Rollen verwalten",
        [AuditView] = "Audit-Log einsehen",
    };
}

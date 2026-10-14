namespace LanBoard.Application.Authorization;

public static class Permissions
{
    public const string PartyManage = "party.manage";
    public const string TournamentManage = "tournament.manage";
    public const string CateringManage = "catering.manage";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
    public const string AuditView = "audit.view";
    public const string ProfileRead = "profile.read";                 // Orga-visible profile values + party overview
    public const string ProfileFieldsManage = "profilefields.manage"; // custom profile field definitions

    // Keys only, in display order; labels are localized in the Web layer (Permission.<key>).
    public static readonly IReadOnlyList<string> All =
    [
        PartyManage,
        TournamentManage,
        CateringManage,
        UsersManage,
        RolesManage,
        AuditView,
        ProfileRead,
        ProfileFieldsManage,
    ];
}

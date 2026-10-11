namespace LanBoard.Web.Auth;

public static class Policies
{
    /// <summary>Satisfied by any user holding at least one permission; gates the admin area entry point.</summary>
    public const string AnyAdmin = "admin.any";
}

namespace LanBoard.Application.Authorization;

public class PermissionDeniedException(string permission)
    : Exception($"Missing permission '{permission}'.")
{
    public string Permission { get; } = permission;
}

using LanBoard.Application.Authorization;
using LanBoard.Core.Entities;

namespace LanBoard.Application.Profiles;

public sealed record Viewer(Guid UserId, IReadOnlySet<string> Permissions);

public static class ProfileVisibility
{
    // Public → everyone signed in; Orga → owner or profile.read; Private → owner or users.manage
    public static bool CanSee(FieldVisibility visibility, Guid ownerId, Viewer viewer)
        => viewer.UserId == ownerId || visibility switch
        {
            FieldVisibility.Public => true,
            FieldVisibility.Orga => viewer.Permissions.Contains(Permissions.ProfileRead),
            FieldVisibility.Private => viewer.Permissions.Contains(Permissions.UsersManage),
            _ => false,
        };
}

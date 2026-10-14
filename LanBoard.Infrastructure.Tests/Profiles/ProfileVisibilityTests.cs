using LanBoard.Application.Authorization;
using LanBoard.Application.Profiles;
using LanBoard.Core.Entities;

namespace LanBoard.Infrastructure.Tests.Profiles;

public class ProfileVisibilityTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    public enum ViewerKind { Owner, Plain, ProfileReader, UserManager }

    private static Viewer CreateViewer(ViewerKind kind) => kind switch
    {
        ViewerKind.Owner => new Viewer(OwnerId, new HashSet<string>()),
        ViewerKind.Plain => new Viewer(Guid.NewGuid(), new HashSet<string>()),
        ViewerKind.ProfileReader => new Viewer(Guid.NewGuid(), new HashSet<string> { Permissions.ProfileRead }),
        ViewerKind.UserManager => new Viewer(Guid.NewGuid(), new HashSet<string> { Permissions.UsersManage }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    [Theory]
    [InlineData(FieldVisibility.Public, ViewerKind.Owner, true)]
    [InlineData(FieldVisibility.Public, ViewerKind.Plain, true)]
    [InlineData(FieldVisibility.Public, ViewerKind.ProfileReader, true)]
    [InlineData(FieldVisibility.Public, ViewerKind.UserManager, true)]
    [InlineData(FieldVisibility.Orga, ViewerKind.Owner, true)]
    [InlineData(FieldVisibility.Orga, ViewerKind.Plain, false)]
    [InlineData(FieldVisibility.Orga, ViewerKind.ProfileReader, true)]
    [InlineData(FieldVisibility.Orga, ViewerKind.UserManager, false)]
    [InlineData(FieldVisibility.Private, ViewerKind.Owner, true)]
    [InlineData(FieldVisibility.Private, ViewerKind.Plain, false)]
    [InlineData(FieldVisibility.Private, ViewerKind.ProfileReader, false)]
    [InlineData(FieldVisibility.Private, ViewerKind.UserManager, true)]
    public void CanSee_Matrix(FieldVisibility visibility, ViewerKind kind, bool expected)
        => Assert.Equal(expected, ProfileVisibility.CanSee(visibility, OwnerId, CreateViewer(kind)));
}

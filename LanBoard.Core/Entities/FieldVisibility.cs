namespace LanBoard.Core.Entities;

// Public: all signed-in users; Orga: owner + profile.read; Private: owner + users.manage
public enum FieldVisibility { Public, Orga, Private }

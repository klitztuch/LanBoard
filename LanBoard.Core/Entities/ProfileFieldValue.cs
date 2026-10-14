namespace LanBoard.Core.Entities;

// Empty values are never stored; clearing a value deletes the row.
public class ProfileFieldValue
{
    public Guid UserId { get; set; }
    public Guid FieldId { get; set; }
    public required string Value { get; set; }

    public ProfileFieldDefinition Field { get; set; } = null!;
}

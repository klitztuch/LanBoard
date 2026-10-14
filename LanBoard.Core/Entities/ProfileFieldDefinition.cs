namespace LanBoard.Core.Entities;

public class ProfileFieldDefinition
{
    public Guid Id { get; set; }
    public required string Key { get; set; }                       // immutable after creation
    public Dictionary<string, string> Labels { get; set; } = [];    // culture → label
    public ProfileFieldType Type { get; set; }                      // immutable after creation
    public List<string> Choices { get; set; } = [];                 // SingleChoice only
    public bool IsRequired { get; set; }
    public FieldVisibility Visibility { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

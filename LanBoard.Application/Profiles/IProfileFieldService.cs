using LanBoard.Core.Entities;

namespace LanBoard.Application.Profiles;

public interface IProfileFieldService
{
    Task<IReadOnlyList<ProfileFieldDefinition>> GetAllAsync(CancellationToken ct = default);
    Task<ProfileFieldDefinition> CreateAsync(ProfileFieldInput input, string key, Guid performedByUserId, CancellationToken ct = default);

    /// <summary>Key and type are immutable: the key isn't part of the input, and <see cref="ProfileFieldInput.Type"/> is ignored.</summary>
    Task UpdateAsync(Guid id, ProfileFieldInput input, Guid performedByUserId, CancellationToken ct = default);

    /// <summary>All user values of the field are removed by the cascading FK.</summary>
    Task DeleteAsync(Guid id, Guid performedByUserId, CancellationToken ct = default);
}

public sealed record ProfileFieldInput(
    IReadOnlyDictionary<string, string> Labels,
    ProfileFieldType Type,
    IReadOnlyList<string> Choices,
    bool IsRequired,
    FieldVisibility Visibility,
    int SortOrder);

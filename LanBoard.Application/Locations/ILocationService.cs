using LanBoard.Core.Entities;

namespace LanBoard.Application.Locations;

public interface ILocationService
{
    Task<IReadOnlyList<Location>> GetAllAsync(CancellationToken ct = default);
    Task<Location> CreateAsync(LocationInput input, Guid performedByUserId, CancellationToken ct = default);
    Task UpdateAsync(Guid id, LocationInput input, Guid performedByUserId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid performedByUserId, CancellationToken ct = default);
}

/// <summary>Editable fields of a <see cref="Location"/>; blank texts are stored as null.</summary>
public sealed record LocationInput(
    string Name,
    string? Address = null,
    double? Latitude = null,
    double? Longitude = null,
    string? Directions = null,
    string? Parking = null,
    string? Notes = null);

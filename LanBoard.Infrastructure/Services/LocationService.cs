using LanBoard.Application.Admin;
using LanBoard.Application.Authorization;
using LanBoard.Application.Errors;
using LanBoard.Application.Interfaces;
using LanBoard.Application.Locations;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LanBoard.Infrastructure.Services;

public class LocationService(
    ILocationRepository locations,
    IAdminAuditLogRepository auditLog,
    IPermissionService permissions) : ILocationService
{
    public const int MaxNameLength = 200;
    public const int MaxTextLength = 4000;

    public Task<IReadOnlyList<Location>> GetAllAsync(CancellationToken ct = default)
        => locations.GetAllAsync(ct);

    public async Task<Location> CreateAsync(LocationInput input, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissions.EnsureAsync(performedByUserId, Permissions.PartyManage, ct);

        var location = new Location { Id = Guid.NewGuid(), Name = "" };
        await ApplyAsync(location, input, ct);
        await locations.AddAsync(location, ct);
        await SaveMappingDuplicateNameAsync(ct);
        await LogAsync(performedByUserId, AuditActions.LocationCreated, $"Location \"{location.Name}\" created", ct);
        return location;
    }

    public async Task UpdateAsync(Guid id, LocationInput input, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissions.EnsureAsync(performedByUserId, Permissions.PartyManage, ct);

        var location = await locations.GetByIdAsync(id, ct)
            ?? throw new BusinessRuleException(ErrorCodes.LocationNotFound);
        var oldName = location.Name;
        await ApplyAsync(location, input, ct);
        await SaveMappingDuplicateNameAsync(ct);

        var renamed = oldName == location.Name ? "" : $" (formerly \"{oldName}\")";
        await LogAsync(performedByUserId, AuditActions.LocationUpdated, $"Location \"{location.Name}\"{renamed} updated", ct);
    }

    public async Task DeleteAsync(Guid id, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissions.EnsureAsync(performedByUserId, Permissions.PartyManage, ct);

        var location = await locations.GetByIdAsync(id, ct)
            ?? throw new BusinessRuleException(ErrorCodes.LocationNotFound);
        // Check-then-act; a party created concurrently is caught by the restricting FK.
        var partyCount = await locations.CountPartiesAsync(id, ct);
        if (partyCount > 0)
            throw new BusinessRuleException(ErrorCodes.LocationInUse, partyCount);

        locations.Remove(location);
        await locations.SaveChangesAsync(ct);
        await LogAsync(performedByUserId, AuditActions.LocationDeleted, $"Location \"{location.Name}\" deleted", ct);
    }

    // Validates everything before touching the (possibly tracked) entity, so a rejected update leaves it unchanged.
    private async Task ApplyAsync(Location location, LocationInput input, CancellationToken ct)
    {
        var name = input.Name.Trim();
        if (name.Length is 0 or > MaxNameLength)
            throw new BusinessRuleException(ErrorCodes.LocationNameInvalid, MaxNameLength);

        var address = NormalizeText(input.Address);
        var directions = NormalizeText(input.Directions);
        var parking = NormalizeText(input.Parking);
        var notes = NormalizeText(input.Notes);

        if (input.Latitude.HasValue != input.Longitude.HasValue
            || input.Latitude is < -90 or > 90
            || input.Longitude is < -180 or > 180)
            throw new BusinessRuleException(ErrorCodes.LocationCoordinatesInvalid);

        // Names are unique case-sensitively: "Garage" and "garage" may coexist (cleaned up by hand).
        if (await locations.GetByNameAsync(name, ct) is { } existing && existing.Id != location.Id)
            throw new BusinessRuleException(ErrorCodes.LocationNameTaken);

        location.Name = name;
        location.Address = address;
        location.Latitude = input.Latitude;
        location.Longitude = input.Longitude;
        location.Directions = directions;
        location.Parking = parking;
        location.Notes = notes;
    }

    private static string? NormalizeText(string? value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (trimmed is { Length: > MaxTextLength })
            throw new BusinessRuleException(ErrorCodes.LocationTextTooLong, MaxTextLength);
        return trimmed;
    }

    private async Task SaveMappingDuplicateNameAsync(CancellationToken ct)
    {
        try
        {
            await locations.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new BusinessRuleException(ErrorCodes.LocationNameTaken);
        }
    }

    private async Task LogAsync(Guid userId, string action, string details, CancellationToken ct)
    {
        await auditLog.AddAsync(new AdminAuditLogEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            Details = details,
            CreatedAt = DateTime.UtcNow
        }, ct);
        await auditLog.SaveChangesAsync(ct);
    }
}

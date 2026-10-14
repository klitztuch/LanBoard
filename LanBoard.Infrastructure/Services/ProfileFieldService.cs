using System.Text.RegularExpressions;
using LanBoard.Application.Admin;
using LanBoard.Application.Authorization;
using LanBoard.Application.Errors;
using LanBoard.Application.Interfaces;
using LanBoard.Application.Profiles;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LanBoard.Infrastructure.Services;

public partial class ProfileFieldService(
    IProfileFieldRepository fields,
    IPermissionService permissionService,
    IAdminAuditLogRepository auditLog) : IProfileFieldService
{
    private const int MaxLabelLength = 100;
    private const int MaxChoices = 50;
    private const int MaxChoiceLength = 50;

    public Task<IReadOnlyList<ProfileFieldDefinition>> GetAllAsync(CancellationToken ct = default)
        => fields.GetAllAsync(ct);

    public async Task<ProfileFieldDefinition> CreateAsync(ProfileFieldInput input, string key, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissionService.EnsureAsync(performedByUserId, Permissions.ProfileFieldsManage, ct);

        var trimmedKey = key.Trim();
        if (!KeyRegex().IsMatch(trimmedKey))
            throw new BusinessRuleException(ErrorCodes.ProfileFieldKeyInvalid);
        var labels = ValidateLabels(input.Labels);
        var choices = ValidateChoices(input.Type, input.Choices);
        if (await fields.KeyExistsAsync(trimmedKey, ct))
            throw new BusinessRuleException(ErrorCodes.ProfileFieldKeyTaken);

        var field = new ProfileFieldDefinition
        {
            Id = Guid.NewGuid(),
            Key = trimmedKey,
            Labels = labels,
            Type = input.Type,
            Choices = choices,
            IsRequired = input.IsRequired,
            Visibility = input.Visibility,
            SortOrder = input.SortOrder,
            CreatedAt = DateTime.UtcNow
        };
        await fields.AddAsync(field, ct);
        await SaveMappingDuplicateKeyAsync(ct);
        await LogAsync(performedByUserId, AuditActions.ProfileFieldCreated, $"Profile field \"{field.Key}\" created ({Describe(field)})", ct);
        return field;
    }

    public async Task UpdateAsync(Guid id, ProfileFieldInput input, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissionService.EnsureAsync(performedByUserId, Permissions.ProfileFieldsManage, ct);

        var field = await fields.GetByIdAsync(id, ct)
            ?? throw new BusinessRuleException(ErrorCodes.ProfileFieldNotFound);

        // Choices are validated against the stored type: changing the type would invalidate existing values.
        var labels = ValidateLabels(input.Labels);
        var choices = ValidateChoices(field.Type, input.Choices);

        var before = Describe(field);
        field.Labels = labels;
        field.Choices = choices;
        field.IsRequired = input.IsRequired;
        field.Visibility = input.Visibility;
        field.SortOrder = input.SortOrder;
        await fields.SaveChangesAsync(ct);
        await LogAsync(performedByUserId, AuditActions.ProfileFieldUpdated, $"Profile field \"{field.Key}\" updated: {before} → {Describe(field)}", ct);
    }

    public async Task DeleteAsync(Guid id, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissionService.EnsureAsync(performedByUserId, Permissions.ProfileFieldsManage, ct);

        var field = await fields.GetByIdAsync(id, ct)
            ?? throw new BusinessRuleException(ErrorCodes.ProfileFieldNotFound);

        // User values are removed by the cascading FK on ProfileFieldValues.FieldId.
        fields.Remove(field);
        await fields.SaveChangesAsync(ct);
        await LogAsync(performedByUserId, AuditActions.ProfileFieldDeleted, $"Profile field \"{field.Key}\" deleted", ct);
    }

    // Empty labels are dropped; label culture keys are checked against SupportedCultures in the Web layer.
    private static Dictionary<string, string> ValidateLabels(IReadOnlyDictionary<string, string> labels)
    {
        var result = labels
            .Select(kv => (Culture: kv.Key, Label: kv.Value.Trim()))
            .Where(l => l.Label.Length > 0)
            .ToDictionary(l => l.Culture, l => l.Label);
        if (result.Count == 0 || result.Values.Any(l => l.Length > MaxLabelLength))
            throw new BusinessRuleException(ErrorCodes.ProfileFieldLabelMissing);
        return result;
    }

    private static List<string> ValidateChoices(ProfileFieldType type, IReadOnlyList<string> choices)
    {
        if (type != ProfileFieldType.SingleChoice)
            return choices.Count == 0 ? [] : throw new BusinessRuleException(ErrorCodes.ProfileFieldChoicesInvalid);

        var trimmed = choices.Select(c => c.Trim()).ToList();
        var valid = trimmed.Count is > 0 and <= MaxChoices
                    && trimmed.All(c => c.Length is > 0 and <= MaxChoiceLength)
                    && trimmed.Distinct(StringComparer.Ordinal).Count() == trimmed.Count;
        return valid ? trimmed : throw new BusinessRuleException(ErrorCodes.ProfileFieldChoicesInvalid);
    }

    private static string Describe(ProfileFieldDefinition field)
    {
        var choices = field.Choices.Count > 0 ? $" [{string.Join(", ", field.Choices)}]" : "";
        var required = field.IsRequired ? ", required" : "";
        return $"{field.Type}{choices}, {field.Visibility}{required}, #{field.SortOrder}";
    }

    private async Task SaveMappingDuplicateKeyAsync(CancellationToken ct)
    {
        try
        {
            await fields.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new BusinessRuleException(ErrorCodes.ProfileFieldKeyTaken);
        }
    }

    private async Task LogAsync(Guid userId, string action, string details, CancellationToken ct)
    {
        await auditLog.AddAsync(new AdminAuditLogEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            Details = details.Length > 500 ? details[..500] : details,   // Details column is varchar(500)
            CreatedAt = DateTime.UtcNow
        }, ct);
        await auditLog.SaveChangesAsync(ct);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,49}$")]
    private static partial Regex KeyRegex();
}

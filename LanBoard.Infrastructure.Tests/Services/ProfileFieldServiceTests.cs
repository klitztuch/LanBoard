using LanBoard.Application.Admin;
using LanBoard.Application.Authorization;
using LanBoard.Application.Errors;
using LanBoard.Application.Interfaces;
using LanBoard.Application.Profiles;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.Services;
using NSubstitute;

namespace LanBoard.Infrastructure.Tests.Services;

public class ProfileFieldServiceTests
{
    private static readonly Guid CallerId = Guid.NewGuid();

    private readonly IProfileFieldRepository _fields = Substitute.For<IProfileFieldRepository>();
    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();
    private readonly IAdminAuditLogRepository _auditLog = Substitute.For<IAdminAuditLogRepository>();
    private readonly ProfileFieldService _sut;

    public ProfileFieldServiceTests()
    {
        _sut = new ProfileFieldService(_fields, _permissions, _auditLog);
        Grant(Permissions.ProfileFieldsManage);
    }

    private void Grant(params string[] granted)
        => _permissions.EnsureAsync(CallerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => granted.Contains(ci.ArgAt<string>(1))
                ? Task.CompletedTask
                : Task.FromException(new PermissionDeniedException(ci.ArgAt<string>(1))));

    private static ProfileFieldInput Input(
        ProfileFieldType type = ProfileFieldType.Text,
        IReadOnlyList<string>? choices = null,
        IReadOnlyDictionary<string, string>? labels = null,
        bool isRequired = false,
        FieldVisibility visibility = FieldVisibility.Public,
        int sortOrder = 0)
        => new(labels ?? new Dictionary<string, string> { ["en"] = "Discord", ["de"] = "Discord" },
            type, choices ?? [], isRequired, visibility, sortOrder);

    private ProfileFieldDefinition GivenField(string key = "shirt-size", ProfileFieldType type = ProfileFieldType.SingleChoice)
    {
        var field = new ProfileFieldDefinition
        {
            Id = Guid.NewGuid(),
            Key = key,
            Labels = new() { ["en"] = "T-shirt size" },
            Type = type,
            Choices = type == ProfileFieldType.SingleChoice ? ["S", "M", "L"] : [],
            Visibility = FieldVisibility.Orga
        };
        _fields.GetByIdAsync(field.Id, Arg.Any<CancellationToken>()).Returns(field);
        return field;
    }

    private Task AssertLoggedAsync(string action, Func<string, bool>? details = null)
        => _auditLog.Received(1).AddAsync(
            Arg.Is<AdminAuditLogEntry>(e => e.UserId == CallerId && e.Action == action && (details == null || details(e.Details!))),
            Arg.Any<CancellationToken>());

    private async Task AssertNothingWrittenAsync()
    {
        await _fields.DidNotReceive().AddAsync(Arg.Any<ProfileFieldDefinition>(), Arg.Any<CancellationToken>());
        await _fields.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLog.DidNotReceive().AddAsync(Arg.Any<AdminAuditLogEntry>(), Arg.Any<CancellationToken>());
    }

    // --- Create ---

    [Fact]
    public async Task CreateAsync_Valid_PersistsAndLogs()
    {
        var field = await _sut.CreateAsync(
            Input(ProfileFieldType.SingleChoice, [" S ", "M"], new Dictionary<string, string> { ["en"] = " Size ", ["de"] = "  " },
                isRequired: true, visibility: FieldVisibility.Orga, sortOrder: 3),
            " shirt-size ", CallerId);

        Assert.Equal("shirt-size", field.Key);
        Assert.Equal(new Dictionary<string, string> { ["en"] = "Size" }, field.Labels);
        Assert.Equal(["S", "M"], field.Choices);
        Assert.True(field.IsRequired);
        Assert.Equal(FieldVisibility.Orga, field.Visibility);
        Assert.Equal(3, field.SortOrder);
        await _fields.Received(1).AddAsync(field, Arg.Any<CancellationToken>());
        await _fields.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await AssertLoggedAsync(AuditActions.ProfileFieldCreated, d => d.Contains("shirt-size"));
    }

    [Fact]
    public async Task CreateAsync_WithoutPermission_ThrowsBeforeAnything()
    {
        Grant();

        await Assert.ThrowsAsync<PermissionDeniedException>(() => _sut.CreateAsync(Input(), "discord", CallerId));

        await _fields.DidNotReceive().KeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await AssertNothingWrittenAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Discord")]
    [InlineData("-discord")]
    [InlineData("discord_tag")]
    [InlineData("disc ord")]
    [InlineData("a12345678901234567890123456789012345678901234567890")] // 51 chars
    public async Task CreateAsync_InvalidKey_Throws(string key)
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(Input(), key, CallerId));

        Assert.Equal(ErrorCodes.ProfileFieldKeyInvalid, ex.Code);
        await AssertNothingWrittenAsync();
    }

    [Theory]
    [InlineData("discord")]
    [InlineData("0")]
    [InlineData("t-shirt-2")]
    public async Task CreateAsync_ValidKey_Accepted(string key)
    {
        var field = await _sut.CreateAsync(Input(), key, CallerId);

        Assert.Equal(key, field.Key);
    }

    [Fact]
    public async Task CreateAsync_KeyTaken_Throws()
    {
        _fields.KeyExistsAsync("discord", Arg.Any<CancellationToken>()).Returns(true);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(Input(), "discord", CallerId));

        Assert.Equal(ErrorCodes.ProfileFieldKeyTaken, ex.Code);
        await AssertNothingWrittenAsync();
    }

    public static TheoryData<Dictionary<string, string>> InvalidLabels => new()
    {
        new Dictionary<string, string>(),
        new Dictionary<string, string> { ["en"] = "  ", ["de"] = "" },
        new Dictionary<string, string> { ["en"] = new string('x', 101) },
    };

    [Theory]
    [MemberData(nameof(InvalidLabels))]
    public async Task CreateAsync_InvalidLabels_Throws(Dictionary<string, string> labels)
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(Input(labels: labels), "discord", CallerId));

        Assert.Equal(ErrorCodes.ProfileFieldLabelMissing, ex.Code);
        await AssertNothingWrittenAsync();
    }

    public static TheoryData<ProfileFieldType, string[]> InvalidChoices => new()
    {
        { ProfileFieldType.SingleChoice, [] },
        { ProfileFieldType.SingleChoice, ["S", " "] },
        { ProfileFieldType.SingleChoice, ["S", " S"] },
        { ProfileFieldType.SingleChoice, [new string('x', 51)] },
        { ProfileFieldType.SingleChoice, Enumerable.Range(0, 51).Select(i => i.ToString()).ToArray() },
        { ProfileFieldType.Text, ["S"] },
        { ProfileFieldType.YesNo, ["yes", "no"] },
    };

    [Theory]
    [MemberData(nameof(InvalidChoices))]
    public async Task CreateAsync_InvalidChoices_Throws(ProfileFieldType type, string[] choices)
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(Input(type, choices), "field", CallerId));

        Assert.Equal(ErrorCodes.ProfileFieldChoicesInvalid, ex.Code);
        await AssertNothingWrittenAsync();
    }

    // --- Update ---

    [Fact]
    public async Task UpdateAsync_Valid_UpdatesMutablePropertiesAndLogs()
    {
        var field = GivenField();

        await _sut.UpdateAsync(field.Id,
            Input(ProfileFieldType.SingleChoice, ["S", "M", "L", "XL"], new Dictionary<string, string> { ["de"] = "T-Shirt-Größe" },
                isRequired: true, visibility: FieldVisibility.Private, sortOrder: 7),
            CallerId);

        Assert.Equal(new Dictionary<string, string> { ["de"] = "T-Shirt-Größe" }, field.Labels);
        Assert.Equal(["S", "M", "L", "XL"], field.Choices);
        Assert.True(field.IsRequired);
        Assert.Equal(FieldVisibility.Private, field.Visibility);
        Assert.Equal(7, field.SortOrder);
        await _fields.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await AssertLoggedAsync(AuditActions.ProfileFieldUpdated, d => d.Contains("shirt-size") && d.Contains("XL"));
    }

    [Fact]
    public async Task UpdateAsync_KeepsKeyAndTypeAndValidatesChoicesAgainstStoredType()
    {
        var field = GivenField("discord", ProfileFieldType.Text);

        // Requested type change to SingleChoice is ignored, so choices are invalid for the stored Text type.
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.UpdateAsync(field.Id, Input(ProfileFieldType.SingleChoice, ["a"]), CallerId));
        Assert.Equal(ErrorCodes.ProfileFieldChoicesInvalid, ex.Code);

        await _sut.UpdateAsync(field.Id, Input(ProfileFieldType.YesNo), CallerId);

        Assert.Equal("discord", field.Key);
        Assert.Equal(ProfileFieldType.Text, field.Type);
    }

    [Fact]
    public async Task UpdateAsync_WithoutPermission_ThrowsBeforeLoading()
    {
        var field = GivenField();
        Grant();

        await Assert.ThrowsAsync<PermissionDeniedException>(() => _sut.UpdateAsync(field.Id, Input(), CallerId));

        await _fields.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task UpdateAsync_UnknownField_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.UpdateAsync(Guid.NewGuid(), Input(), CallerId));

        Assert.Equal(ErrorCodes.ProfileFieldNotFound, ex.Code);
        await AssertNothingWrittenAsync();
    }

    // --- Delete ---

    [Fact]
    public async Task DeleteAsync_Existing_RemovesAndLogs()
    {
        var field = GivenField();

        await _sut.DeleteAsync(field.Id, CallerId);

        _fields.Received(1).Remove(field);
        await _fields.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await AssertLoggedAsync(AuditActions.ProfileFieldDeleted, d => d.Contains("shirt-size"));
    }

    [Fact]
    public async Task DeleteAsync_WithoutPermission_ThrowsBeforeLoading()
    {
        var field = GivenField();
        Grant();

        await Assert.ThrowsAsync<PermissionDeniedException>(() => _sut.DeleteAsync(field.Id, CallerId));

        _fields.DidNotReceive().Remove(Arg.Any<ProfileFieldDefinition>());
        await AssertNothingWrittenAsync();
    }

    [Fact]
    public async Task DeleteAsync_UnknownField_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.DeleteAsync(Guid.NewGuid(), CallerId));

        Assert.Equal(ErrorCodes.ProfileFieldNotFound, ex.Code);
        _fields.DidNotReceive().Remove(Arg.Any<ProfileFieldDefinition>());
    }
}

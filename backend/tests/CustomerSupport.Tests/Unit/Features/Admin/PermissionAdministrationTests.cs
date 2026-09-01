using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Admin.Commands.RevokePermission;
using CustomerSupport.Application.Features.Admin.Commands.SetRolePermissions;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using FluentAssertions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Features.Admin;

public sealed class PermissionAdministrationTests
{
    [Fact]
    public async Task Revoke_LastBuiltInPermission_ReturnsConflict()
    {
        var service = new Mock<IPermissionAdministrationService>();
        var messages = new Mock<IMessageFactory>();
        messages.Setup(x => x.Fail<MediatR.Unit>(ApplicationErrors.Permission.LAST_REQUIRED, MessageType.Conflict))
            .Returns(Response<MediatR.Unit>.Fail(ApplicationErrors.Permission.LAST_REQUIRED, "last", MessageType.Conflict));
        service.Setup(x => x.RevokeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PermissionMutationResult.LastPermissionRequired);

        var result = await new RevokePermissionCommandHandler(service.Object, messages.Object)
            .Handle(new RevokePermissionCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Permission.LAST_REQUIRED);
    }

    [Fact]
    public void Revoke_EmptyIdentifiers_IsInvalid()
    {
        var result = new RevokePermissionCommandValidator().Validate(
            new RevokePermissionCommand(Guid.Empty, Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    /// <summary>
    /// A well-formed command by default. Callers override individual fields; note that passing
    /// <c>null</c> for <paramref name="permissionIds"/>/<paramref name="expected"/> here means
    /// "use the default", not "send null" — the two null-specific tests below construct the command
    /// directly instead, since a defaulting helper cannot distinguish "not specified" from
    /// "explicitly null".
    /// </summary>
    private static SetRolePermissionsCommand Command(
        IReadOnlyList<Guid>? permissionIds = null,
        IReadOnlyList<Guid>? expected = null,
        Guid? roleId = null)
        => new(roleId ?? Guid.NewGuid(), permissionIds ?? [Guid.NewGuid()], expected ?? []);

    [Fact] // AC-806.6
    [Trait("AC", "806.6")]
    public void Set_NullPermissionIds_IsRefusedWithAFieldError()
    {
        var result = new SetRolePermissionsCommandValidator().Validate(
            new SetRolePermissionsCommand(Guid.NewGuid(), null, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.PropertyName == "PermissionIds")
            .Which.ErrorCode.Should().Be(ApplicationErrors.Validation.PERMISSION_SET_INVALID);
    }

    [Fact] // AC-806.6
    [Trait("AC", "806.6")]
    public void Set_NullExpectedPermissionIds_IsRefusedWithAFieldError()
    {
        var result = new SetRolePermissionsCommandValidator().Validate(
            new SetRolePermissionsCommand(Guid.NewGuid(), [Guid.NewGuid()], null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.PropertyName == "ExpectedPermissionIds")
            .Which.ErrorCode.Should().Be(ApplicationErrors.Validation.PERMISSION_SNAPSHOT_REQUIRED);
    }

    [Fact] // AC-806.6
    [Trait("AC", "806.6")]
    public void Set_DuplicatePermissionIds_IsRefused()
    {
        var duplicated = Guid.NewGuid();

        var result = new SetRolePermissionsCommandValidator()
            .Validate(Command(permissionIds: [duplicated, duplicated]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "PermissionIds");
    }

    [Fact] // AC-806.6
    [Trait("AC", "806.6")]
    public void Set_EmptyGuidInPermissionIds_IsRefused()
    {
        var result = new SetRolePermissionsCommandValidator()
            .Validate(Command(permissionIds: [Guid.Empty]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName.StartsWith("PermissionIds"));
    }

    [Fact] // AC-806.4, AC-806.6
    [Trait("AC", "806.6")]
    public void Set_EmptyRoleId_IsRefused()
    {
        var result = new SetRolePermissionsCommandValidator().Validate(Command(roleId: Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "RoleId");
    }

    [Fact] // AC-806.2 — an empty set is a legal *request*; refusing it is the service's job, not the validator's
    [Trait("AC", "806.2")]
    public void Set_EmptyPermissionIds_PassesValidation()
    {
        var result = new SetRolePermissionsCommandValidator().Validate(Command(permissionIds: []));

        result.IsValid.Should().BeTrue();
    }

    private static (Mock<IPermissionAdministrationService> Service, Mock<IMessageFactory> Messages) SetMocks(
        PermissionMutationResult outcome)
    {
        var service = new Mock<IPermissionAdministrationService>();
        service.Setup(x => x.SetAsync(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome);

        var messages = new Mock<IMessageFactory>();
        messages.Setup(x => x.NotFound<MediatR.Unit>(It.IsAny<string>()))
            .Returns((string key) => Response<MediatR.Unit>.Fail(key, key, MessageType.NotFound));
        messages.Setup(x => x.Fail<MediatR.Unit>(It.IsAny<string>(), It.IsAny<MessageType>()))
            .Returns((string key, MessageType type) => Response<MediatR.Unit>.Fail(key, key, type));
        messages.Setup(x => x.Success(It.IsAny<MediatR.Unit>(), It.IsAny<string>()))
            .Returns((MediatR.Unit data, string key) => Response<MediatR.Unit>.Ok(data, key, key));

        return (service, messages);
    }

    private static Task<Response<MediatR.Unit>> HandleSet(PermissionMutationResult outcome)
    {
        var (service, messages) = SetMocks(outcome);
        return new SetRolePermissionsCommandHandler(service.Object, messages.Object).Handle(
            new SetRolePermissionsCommand(Guid.NewGuid(), [Guid.NewGuid()], []),
            CancellationToken.None);
    }

    [Fact] // AC-806.1
    [Trait("AC", "806.1")]
    public async Task Set_Succeeded_ReturnsUpdatedConfirmation()
    {
        var result = await HandleSet(PermissionMutationResult.Succeeded);

        result.Success.Should().BeTrue();
        result.Code.Should().Be(ApplicationErrors.Permission.UPDATED);
    }

    [Fact] // AC-806.4
    [Trait("AC", "806.4")]
    public async Task Set_RoleNotFound_ReturnsNotFound()
    {
        var result = await HandleSet(PermissionMutationResult.RoleNotFound);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Permission.ROLE_NOT_FOUND);
    }

    [Fact] // AC-806.3
    [Trait("AC", "806.3")]
    public async Task Set_UnknownPermission_ReturnsNotFound()
    {
        var result = await HandleSet(PermissionMutationResult.PermissionNotFound);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Permission.NOT_FOUND);
    }

    [Fact] // AC-806.5
    [Trait("AC", "806.5")]
    public async Task Set_StaleSnapshot_ReturnsConflict()
    {
        var result = await HandleSet(PermissionMutationResult.StaleSnapshot);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Permission.STALE_SNAPSHOT);
    }

    [Fact] // AC-806.2
    [Trait("AC", "806.2")]
    public async Task Set_WouldEmptyBuiltInRole_ReturnsConflict()
    {
        var result = await HandleSet(PermissionMutationResult.LastPermissionRequired);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Permission.LAST_REQUIRED);
    }
}

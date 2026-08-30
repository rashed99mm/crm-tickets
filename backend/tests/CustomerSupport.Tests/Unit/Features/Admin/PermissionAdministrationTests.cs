using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Admin.Commands.RevokePermission;
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
}

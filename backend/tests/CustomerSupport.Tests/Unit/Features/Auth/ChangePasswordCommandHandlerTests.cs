using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Auth.Commands.ChangePassword;
using CustomerSupport.Application.Features.Auth.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Features.Auth;

public class ChangePasswordCommandHandlerTests
{
    private readonly Mock<IIdentityUserService> _identityUserService = new();
    private readonly Mock<IRefreshTokenService> _refreshTokenService = new();
    private readonly Mock<IMessageFactory> _messages = new();
    private readonly Mock<ILogger<ChangePasswordCommandHandler>> _logger = new();
    private readonly ChangePasswordCommandHandler _handler;

    private static readonly ApplicationUser TestUser =
        ApplicationUser.Create("dana@example.com", "dana", "Dana", "Support");

    public ChangePasswordCommandHandlerTests()
    {
        _messages
            .Setup(m => m.Success(It.IsAny<MediatR.Unit>(), It.IsAny<string>()))
            .Returns((MediatR.Unit val, string code) => Response<MediatR.Unit>.Ok(val, code, "OK"));
        _messages
            .Setup(m => m.Fail<MediatR.Unit>(It.IsAny<string>(), It.IsAny<MessageType>()))
            .Returns((string code, MessageType type) => Response<MediatR.Unit>.Fail(code, code, type));
        _messages
            .Setup(m => m.Fail<MediatR.Unit>(It.IsAny<string>(), It.IsAny<MessageType>(), It.IsAny<IList<FieldError>>()))
            .Returns((string code, MessageType type, IList<FieldError> errors) => Response<MediatR.Unit>.Fail(code, code, type, errors));
        _messages
            .Setup(m => m.NotFound<MediatR.Unit>(It.IsAny<string>()))
            .Returns((string code) => Response<MediatR.Unit>.Fail(code, code, MessageType.NotFound));

        _handler = new ChangePasswordCommandHandler(
            _identityUserService.Object,
            _refreshTokenService.Object,
            _messages.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Handle_WrongCurrentPassword_ReturnsValidationErrorKeyedToCurrentPassword()
    {
        _identityUserService
            .Setup(s => s.FindByIdAsync(TestUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestUser);
        _identityUserService
            .Setup(s => s.ChangePasswordAsync(TestUser, "wrong-current", "New-Password-9"))
            .ReturnsAsync(IdentityOperationResult.Failure(
                new[] { ("PasswordMismatch", "Incorrect password.") }));

        var command = new ChangePasswordCommand(TestUser.Id, "wrong-current", "New-Password-9");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Auth.CURRENT_PASSWORD_INCORRECT);
        result.Errors.Should().Contain(e => e.Field == "currentPassword");

        _refreshTokenService.Verify(
            r => r.RevokeAllUserRefreshTokensAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WeakNewPassword_ReturnsValidationErrorKeyedToNewPassword()
    {
        _identityUserService
            .Setup(s => s.FindByIdAsync(TestUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestUser);
        _identityUserService
            .Setup(s => s.ChangePasswordAsync(TestUser, "Current-Password-1", "weak"))
            .ReturnsAsync(IdentityOperationResult.Failure(
                new[] { ("PasswordTooShort", "Passwords must be at least 8 characters.") }));

        var command = new ChangePasswordCommand(TestUser.Id, "Current-Password-1", "weak");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Auth.PASSWORD_TOO_WEAK);
        result.Errors.Should().Contain(e => e.Field == "newPassword");
    }

    [Fact]
    public async Task Handle_Success_RevokesAllRefreshTokensAndReturnsSuccess()
    {
        _identityUserService
            .Setup(s => s.FindByIdAsync(TestUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestUser);
        _identityUserService
            .Setup(s => s.ChangePasswordAsync(TestUser, "Current-Password-1", "New-Password-9"))
            .ReturnsAsync(IdentityOperationResult.Success());

        var command = new ChangePasswordCommand(TestUser.Id, "Current-Password-1", "New-Password-9");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();

        _refreshTokenService.Verify(
            r => r.RevokeAllUserRefreshTokensAsync(TestUser.Id, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownUser_ReturnsUnauthorized()
    {
        _identityUserService
            .Setup(s => s.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationUser?)null);

        var command = new ChangePasswordCommand(Guid.NewGuid(), "whatever", "New-Password-9");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(ApplicationErrors.Auth.NOT_AUTHENTICATED);
    }
}

using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Verification.Commands.VerifyOtp;
using CustomerSupport.Application.Features.Verification.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Entities.Verification;
using FluentAssertions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Features.Verification;

/// <summary>Handler-level proof of the verify behaviour (AC-439..AC-445).</summary>
public class VerifyOtpCommandHandlerTests
{
    private readonly Mock<IUserContext> _user = new();
    private readonly Mock<IOtpVerificationRepository> _repo = new();
    private readonly Mock<IOtpCodeHasher> _hasher = new();
    private readonly Mock<IIdentityUserService> _identity = new();
    private readonly Mock<IMessageFactory> _messages = new();

    public VerifyOtpCommandHandlerTests()
    {
        _messages.Setup(m => m.Success(It.IsAny<VerifyOtpResponse>(), It.IsAny<string>()))
            .Returns((VerifyOtpResponse d, string k) => Response<VerifyOtpResponse>.Ok(d, k, "ok"));
        _messages.Setup(m => m.Fail<VerifyOtpResponse>(It.IsAny<string>(), It.IsAny<MessageType>()))
            .Returns((string k, MessageType t) => Response<VerifyOtpResponse>.Fail(k, "fail", t));
    }

    private VerifyOtpCommandHandler Handler => new(
        _user.Object, _repo.Object, _hasher.Object, _identity.Object, _messages.Object,
        Mock.Of<Microsoft.Extensions.Logging.ILogger<VerifyOtpCommandHandler>>());

    [Fact]
    public async Task CorrectCode_ConfirmsPhoneAndReturnsSuccess() // AC-439
    {
        var userId = Guid.NewGuid();
        _user.Setup(u => u.IsAuthenticated).Returns(true);
        _user.Setup(u => u.UserId).Returns(userId);

        var v = OtpVerification.Create(userId, "+14155550100", OtpVerificationType.Phone, "hash", DateTime.UtcNow.AddMinutes(5), DateTime.UtcNow);
        _repo.Setup(r => r.GetByIdAsync(v.Id, It.IsAny<CancellationToken>())).ReturnsAsync(v);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var user = ApplicationUser.Create("o@t.local", "o@t.local", "O", "T");
        user.PhoneNumber = "+14155550100";
        _identity.Setup(i => i.FindByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("123456", v.CodeHash)).Returns(true);

        var result = await Handler.Handle(new VerifyOtpCommand(v.Id, "123456"), CancellationToken.None);

        result.Success.Should().BeTrue();
        user.PhoneNumberConfirmed.Should().BeTrue();
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WrongCode_DoesNotConfirmPhone() // AC-440
    {
        var userId = Guid.NewGuid();
        _user.Setup(u => u.IsAuthenticated).Returns(true);
        _user.Setup(u => u.UserId).Returns(userId);

        var v = OtpVerification.Create(userId, "+14155550100", OtpVerificationType.Phone, "hash", DateTime.UtcNow.AddMinutes(5), DateTime.UtcNow);
        _repo.Setup(r => r.GetByIdAsync(v.Id, It.IsAny<CancellationToken>())).ReturnsAsync(v);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var user = ApplicationUser.Create("o@t.local", "o@t.local", "O", "T");
        _identity.Setup(i => i.FindByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("000000", v.CodeHash)).Returns(false);

        var result = await Handler.Handle(new VerifyOtpCommand(v.Id, "000000"), CancellationToken.None);

        result.Success.Should().BeFalse();
        user.PhoneNumberConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task LockedRecord_IsNeverCompared() // AC-441
    {
        var userId = Guid.NewGuid();
        _user.Setup(u => u.IsAuthenticated).Returns(true);
        _user.Setup(u => u.UserId).Returns(userId);

        var v = OtpVerification.Create(userId, "+14155550100", OtpVerificationType.Phone, "hash", DateTime.UtcNow.AddMinutes(5), DateTime.UtcNow);
        for (var i = 0; i < OtpVerification.MaxFailedAttempts; i++)
        {
            v.RegisterFailedAttempt();
        }

        _repo.Setup(r => r.GetByIdAsync(v.Id, It.IsAny<CancellationToken>())).ReturnsAsync(v);

        var result = await Handler.Handle(new VerifyOtpCommand(v.Id, "123456"), CancellationToken.None);

        result.Success.Should().BeFalse();
        _hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UnknownOrOtherUser_ReturnsSafeFailure() // AC-443
    {
        var userId = Guid.NewGuid();
        _user.Setup(u => u.IsAuthenticated).Returns(true);
        _user.Setup(u => u.UserId).Returns(userId);

        // A record that belongs to a different user.
        var other = Guid.NewGuid();
        var v = OtpVerification.Create(other, "+14155550100", OtpVerificationType.Phone, "hash", DateTime.UtcNow.AddMinutes(5), DateTime.UtcNow);
        _repo.Setup(r => r.GetByIdAsync(v.Id, It.IsAny<CancellationToken>())).ReturnsAsync(v);

        var result = await Handler.Handle(new VerifyOtpCommand(v.Id, "123456"), CancellationToken.None);

        result.Success.Should().BeFalse();
        _hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _identity.Verify(i => i.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExpiredRecord_ReturnsSafeFailure() // AC-440
    {
        var userId = Guid.NewGuid();
        _user.Setup(u => u.IsAuthenticated).Returns(true);
        _user.Setup(u => u.UserId).Returns(userId);

        var v = OtpVerification.Create(userId, "+14155550100", OtpVerificationType.Phone, "hash", DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow);
        _repo.Setup(r => r.GetByIdAsync(v.Id, It.IsAny<CancellationToken>())).ReturnsAsync(v);

        var result = await Handler.Handle(new VerifyOtpCommand(v.Id, "123456"), CancellationToken.None);

        result.Success.Should().BeFalse();
        _hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Unauthenticated_ReturnsFailure() // AC-431 analogue
    {
        _user.Setup(u => u.IsAuthenticated).Returns(false);

        var result = await Handler.Handle(new VerifyOtpCommand(Guid.NewGuid(), "123456"), CancellationToken.None);

        result.Success.Should().BeFalse();
        _repo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

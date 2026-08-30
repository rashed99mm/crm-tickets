using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Verification.Commands.RequestOtp;
using CustomerSupport.Application.Features.Verification.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Verification;
using CustomerSupport.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Features.Verification;

/// <summary>Handler-level proof of the request-OTP behaviour (OTP-1, OTP-2, OTP-3, OTP-9).</summary>
public class RequestOtpCommandHandlerTests
{
    private readonly Mock<IUserContext> _user = new();
    private readonly Mock<IOtpVerificationRepository> _repo = new();
    private readonly Mock<IOtpCodeHasher> _hasher = new();
    private readonly Mock<IOtpCodeGenerator> _generator = new();
    private readonly Mock<INotificationGateway> _gateway = new();
    private readonly Mock<IMessageFactory> _messages = new();

    private readonly List<NotificationDispatchRequest> _dispatched = new();

    public RequestOtpCommandHandlerTests()
    {
        _messages.Setup(m => m.Success(It.IsAny<RequestOtpResponse>(), It.IsAny<string>()))
            .Returns((RequestOtpResponse d, string k) => Response<RequestOtpResponse>.Ok(d, k, "ok"));
        _messages.Setup(m => m.Fail<RequestOtpResponse>(It.IsAny<string>(), It.IsAny<MessageType>()))
            .Returns((string k, MessageType t) => Response<RequestOtpResponse>.Fail(k, "fail", t));
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("stored-hash");
        _generator.Setup(g => g.Generate(It.IsAny<int>())).Returns("654321");
        _gateway.Setup(g => g.SendAsync(It.IsAny<NotificationDispatchRequest>(), It.IsAny<CancellationToken>()))
            .Returns<NotificationDispatchRequest, CancellationToken>((req, ct) =>
            {
                _dispatched.Add(req);
                var results = req.Channels.Select(c => new ChannelSendResult(c, true)).ToList();
                return Task.FromResult(new NotificationDispatchResult(true, results));
            });
    }

    private RequestOtpCommandHandler Handler => new(
        _user.Object, _repo.Object, _hasher.Object, _generator.Object, _gateway.Object, _messages.Object,
        Mock.Of<Microsoft.Extensions.Logging.ILogger<RequestOtpCommandHandler>>());

    private void Authenticated(Guid? userId = null)
    {
        _user.Setup(u => u.IsAuthenticated).Returns(true);
        _user.Setup(u => u.UserId).Returns(userId ?? Guid.NewGuid());
    }

    [Fact]
    public async Task Email_DispatchesThroughEmailChannelAndPersistsHashedRecord() // OTP-1
    {
        Authenticated();
        var now = DateTime.UtcNow;

        OtpVerification? stored = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<OtpVerification>(), It.IsAny<CancellationToken>()))
            .Callback<OtpVerification, CancellationToken>((v, _) => stored = v)
            .Returns(Task.CompletedTask);

        var result = await Handler.Handle(new RequestOtpCommand("Owner@Test.LOCAL", OtpVerificationType.Email), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.VerificationId.Should().NotBeEmpty();
        result.Data.Channel.Should().Be(NotificationChannel.Email.Value);

        _gateway.Verify(g => g.SendAsync(It.IsAny<NotificationDispatchRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        var dispatch = _dispatched.Single();
        dispatch.Channels.Should().ContainSingle(c => c == NotificationChannel.Email);
        dispatch.Email.Should().Be("owner@test.local");
        dispatch.Variables["Code"].Should().Be("654321");

        stored.Should().NotBeNull();
        stored!.UserId.Should().Be(_user.Object.UserId);
        stored.Contact.Should().Be("owner@test.local");
        stored.CodeHash.Should().Be("stored-hash");
        stored.CodeHash.Should().NotContain("654321");
        stored.ExpiresAtUtc.Should().BeCloseTo(now + OtpVerification.CodeLifetime, TimeSpan.FromMinutes(1));
        stored.IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task Phone_DispatchesThroughSmsChannelWithPhoneNumber() // OTP-2
    {
        Authenticated();

        var result = await Handler.Handle(new RequestOtpCommand("+14155550100", OtpVerificationType.Phone), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Channel.Should().Be(NotificationChannel.Sms.Value);
        var dispatch = _dispatched.Single();
        dispatch.Channels.Should().ContainSingle(c => c == NotificationChannel.Sms);
        dispatch.PhoneNumber.Should().Be("+14155550100");
        dispatch.Email.Should().BeNull();
        _generator.Verify(g => g.Generate(OtpVerification.CodeLength), Times.Once);
    }

    [Fact]
    public async Task WithinCooldown_RefusesWithoutGeneratingOrSending() // OTP-3
    {
        Authenticated();
        var now = DateTime.UtcNow;
        var recent = OtpVerification.Create(
            _user.Object.UserId, "+14155550100", OtpVerificationType.Phone, "hash",
            now.AddMinutes(5), now);
        _repo.Setup(r => r.GetLatestForUserAsync(
                _user.Object.UserId, "+14155550100", OtpVerificationType.Phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recent);

        var result = await Handler.Handle(new RequestOtpCommand("+14155550100", OtpVerificationType.Phone), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemCodeMap.Resolve(ApplicationErrors.Verification.COOLDOWN));
        _generator.Verify(g => g.Generate(It.IsAny<int>()), Times.Never);
        _gateway.Verify(g => g.SendAsync(It.IsAny<NotificationDispatchRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.AddAsync(It.IsAny<OtpVerification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AfterCooldownElapses_NewRequestIsAccepted() // OTP-3
    {
        Authenticated();
        var now = DateTime.UtcNow;
        var stale = OtpVerification.Create(
            _user.Object.UserId, "+14155550100", OtpVerificationType.Phone, "hash",
            now, now.AddSeconds(-OtpVerification.ResendCooldownSeconds - 1));
        _repo.Setup(r => r.GetLatestForUserAsync(
                _user.Object.UserId, "+14155550100", OtpVerificationType.Phone, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stale);
        _repo.Setup(r => r.AddAsync(It.IsAny<OtpVerification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Handler.Handle(new RequestOtpCommand("+14155550100", OtpVerificationType.Phone), CancellationToken.None);

        result.Success.Should().BeTrue();
        _gateway.Verify(g => g.SendAsync(It.IsAny<NotificationDispatchRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GatewayRefusal_PersistsNothingAndReturnsSafeFailure() // OTP-9
    {
        Authenticated();
        _gateway.Setup(g => g.SendAsync(It.IsAny<NotificationDispatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDispatchResult(false, new[]
            {
                new ChannelSendResult(NotificationChannel.Email, false, ApplicationErrors.Notification.DELIVERY_FAILED),
            }));

        var result = await Handler.Handle(new RequestOtpCommand("owner@test.local", OtpVerificationType.Email), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemCodeMap.Resolve(ApplicationErrors.Verification.DISPATCH_FAILED));
        _repo.Verify(r => r.AddAsync(It.IsAny<OtpVerification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GatewayThrows_SafeFailureAndNothingPersisted() // OTP-9
    {
        Authenticated();
        _gateway.Setup(g => g.SendAsync(It.IsAny<NotificationDispatchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("provider timeout"));

        var result = await Handler.Handle(new RequestOtpCommand("owner@test.local", OtpVerificationType.Email), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemCodeMap.Resolve(ApplicationErrors.Verification.DISPATCH_FAILED));
        _repo.Verify(r => r.AddAsync(It.IsAny<OtpVerification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unauthenticated_ReturnsFailureWithoutTouchingRepositories() // AC-431 analogue
    {
        _user.Setup(u => u.IsAuthenticated).Returns(false);

        var result = await Handler.Handle(new RequestOtpCommand("owner@test.local", OtpVerificationType.Email), CancellationToken.None);

        result.Success.Should().BeFalse();
        _gateway.Verify(g => g.SendAsync(It.IsAny<NotificationDispatchRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.AddAsync(It.IsAny<OtpVerification>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
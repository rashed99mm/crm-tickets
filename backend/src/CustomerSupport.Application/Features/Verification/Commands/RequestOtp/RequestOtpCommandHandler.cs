using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Verification.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Verification;
using CustomerSupport.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Verification.Commands.RequestOtp;

/// <summary>
/// Issues a fresh verification code for the caller's own contact and dispatches it through the
/// notification gateway on the matching channel (Email/SMS).
///
/// Cooldown (OTP-3): the newest record for (user, contact, type) anchors a 60-second refusal
/// window — a request inside it returns a safe cooldown failure without generating or sending
/// anything. Dispatch (OTP-1/OTP-2): the plaintext code exists only inside the gateway request; the
/// record persisted afterwards holds a one-way hash. Safety (OTP-9): a provider failure, timeout or
/// missing integration returns the same safe failure envelope and no record is persisted.
/// </summary>
public class RequestOtpCommandHandler
    : ICommandHandler<RequestOtpCommand, Response<RequestOtpResponse>>
{
    private readonly IUserContext _userContext;
    private readonly IOtpVerificationRepository _otpRepository;
    private readonly IOtpCodeHasher _codeHasher;
    private readonly IOtpCodeGenerator _codeGenerator;
    private readonly INotificationGateway _gateway;
    private readonly IMessageFactory _messages;
    private readonly ILogger<RequestOtpCommandHandler> _logger;

    public RequestOtpCommandHandler(
        IUserContext userContext,
        IOtpVerificationRepository otpRepository,
        IOtpCodeHasher codeHasher,
        IOtpCodeGenerator codeGenerator,
        INotificationGateway gateway,
        IMessageFactory messages,
        ILogger<RequestOtpCommandHandler> logger)
    {
        _userContext = userContext;
        _otpRepository = otpRepository;
        _codeHasher = codeHasher;
        _codeGenerator = codeGenerator;
        _gateway = gateway;
        _messages = messages;
        _logger = logger;
    }

    public async Task<Response<RequestOtpResponse>> Handle(RequestOtpCommand request, CancellationToken ct)
    {
        if (!_userContext.IsAuthenticated || _userContext.UserId == Guid.Empty)
        {
            return _messages.Fail<RequestOtpResponse>(ApplicationErrors.Auth.NOT_AUTHENTICATED, MessageType.Unauthorized);
        }

        var contact = Normalize(request.Contact, request.Type);
        var now = DateTime.UtcNow;

        // OTP-3: a request inside the cooldown window for this contact and channel is refused
        // before generation, hashing or any provider contact.
        var latest = await _otpRepository.GetLatestForUserAsync(_userContext.UserId, contact, request.Type, ct);
        if (latest is not null && !latest.CanRequest(now))
        {
            _logger.LogInformation("OTP request refused — cooldown active for user {UserId} ({Type})", _userContext.UserId, request.Type);
            return _messages.Fail<RequestOtpResponse>(ApplicationErrors.Verification.COOLDOWN, MessageType.BusinessRule);
        }

        var channel = request.Type == OtpVerificationType.Email
            ? NotificationChannel.Email
            : NotificationChannel.Sms;

        var code = _codeGenerator.Generate(OtpVerification.CodeLength);
        var codeHash = _codeHasher.Hash(code);

        NotificationDispatchResult dispatched;
        try
        {
            dispatched = await _gateway.SendAsync(new NotificationDispatchRequest(
                TemplateCode: "OTP_VERIFICATION",
                RecipientUserId: _userContext.UserId,
                Channels: new[] { channel },
                Variables: OtpVariables(code),
                Email: request.Type == OtpVerificationType.Email ? contact : null,
                PhoneNumber: request.Type == OtpVerificationType.Phone ? contact : null,
                BypassUserSettings: true,
                DeduplicationKey: null,
                CorrelationId: null), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // OTP-9 — a thrown provider error is indistinguishable from a refused dispatch.
            _logger.LogError(ex, "OTP dispatch failed for user {UserId} ({Type})", _userContext.UserId, request.Type);
            return _messages.Fail<RequestOtpResponse>(ApplicationErrors.Verification.DISPATCH_FAILED, MessageType.Internal);
        }

        if (!dispatched.Succeeded)
        {
            _logger.LogWarning("OTP dispatch not accepted for user {UserId} ({Type})", _userContext.UserId, request.Type);
            return _messages.Fail<RequestOtpResponse>(ApplicationErrors.Verification.DISPATCH_FAILED, MessageType.Internal);
        }

        // OTP-9 — the record (whose CodeHash was derived from the discarded plaintext) is persisted
        // only after the gateway accepted the dispatch.
        var verification = OtpVerification.Create(
            _userContext.UserId,
            contact,
            request.Type,
            codeHash,
            expiresAtUtc: now + OtpVerification.CodeLifetime,
            createdAtUtc: now);

        try
        {
            await _otpRepository.AddAsync(verification, ct);
        }
        catch (ConcurrencyException)
        {
            return _messages.Fail<RequestOtpResponse>(ApplicationErrors.Verification.DISPATCH_FAILED, MessageType.Internal);
        }

        _logger.LogInformation("OTP dispatched for user {UserId} ({Type})", _userContext.UserId, request.Type);
        return _messages.Success(
            new RequestOtpResponse(verification.Id, verification.ExpiresAtUtc, verification.RetryAfterSeconds(now), channel.Value),
            ApplicationErrors.Verification.REQUESTED);
    }

    private static string Normalize(string contact, OtpVerificationType type)
    {
        var trimmed = contact.Trim();
        return type == OtpVerificationType.Email ? trimmed.ToLowerInvariant() : trimmed;
    }

    private static IReadOnlyDictionary<string, string> OtpVariables(string code) =>
        new Dictionary<string, string>
        {
            ["Code"] = code,
            ["Title"] = "Verification code",
            ["Message"] = "Your verification code is {{Code}}. It expires in five minutes.",
        };
}
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.Features.Verification.Dtos;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Entities.Verification;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Features.Verification.Commands.VerifyOtp;

/// <summary>
/// Confirms a pending OTP and, on success, flips the linked Identity confirmation flag.
///
/// Every unusable state — wrong, malformed (caught earlier), expired, invalidated, locked,
/// unknown id, or a record owned by another user — returns one identical safe failure, so the
/// response never reveals which condition occurred (AC-440, AC-443). A locked record is never
/// compared (AC-441), and the verification write plus the Identity confirmation write share the
/// host's scoped <c>AppDbContext</c>, so they commit atomically (AC-444). The rowversion token
/// makes a second concurrent success lose the race and fall back to an idempotent success
/// (AC-442).
/// </summary>
public class VerifyOtpCommandHandler
    : ICommandHandler<VerifyOtpCommand, Response<VerifyOtpResponse>>
{
    private readonly IUserContext _userContext;
    private readonly IOtpVerificationRepository _otpRepository;
    private readonly IOtpCodeHasher _codeHasher;
    private readonly IIdentityUserService _identityUserService;
    private readonly IMessageFactory _messages;
    private readonly ILogger<VerifyOtpCommandHandler> _logger;

    public VerifyOtpCommandHandler(
        IUserContext userContext,
        IOtpVerificationRepository otpRepository,
        IOtpCodeHasher codeHasher,
        IIdentityUserService identityUserService,
        IMessageFactory messages,
        ILogger<VerifyOtpCommandHandler> logger)
    {
        _userContext = userContext;
        _otpRepository = otpRepository;
        _codeHasher = codeHasher;
        _identityUserService = identityUserService;
        _messages = messages;
        _logger = logger;
    }

    public async Task<Response<VerifyOtpResponse>> Handle(VerifyOtpCommand request, CancellationToken ct)
    {
        if (!_userContext.IsAuthenticated || _userContext.UserId == Guid.Empty)
        {
            return _messages.Fail<VerifyOtpResponse>(ApplicationErrors.Auth.NOT_AUTHENTICATED, MessageType.Unauthorized);
        }

        OtpVerification? verification;
        try
        {
            verification = await _otpRepository.GetByIdAsync(request.VerificationId, ct);
        }
        catch (ConcurrencyException)
        {
            // Another request completed (or invalidated) the record first.
            return SafeFailure();
        }

        // AC-443: an unknown id and a record belonging to another user produce the identical response.
        if (verification is null || verification.UserId != _userContext.UserId)
        {
            _logger.LogInformation("OTP verify refused — no usable record for {VerificationId}", request.VerificationId);
            return SafeFailure();
        }

        var now = DateTime.UtcNow;

        // AC-440 / AC-441: any unusable state is a safe failure; a locked record is never compared.
        if (!verification.CanAttempt(now))
        {
            return SafeFailure();
        }

        // Defensive: the validator already enforces six digits, but never trust the boundary.
        if (!IsSixDigits(request.Code) || !_codeHasher.Verify(request.Code, verification.CodeHash))
        {
            verification.RegisterFailedAttempt();
            try
            {
                await _otpRepository.SaveChangesAsync(ct);
            }
            catch (ConcurrencyException)
            {
                return SafeFailure();
            }

            _logger.LogInformation("OTP verify failed for user {UserId}", _userContext.UserId);
            return SafeFailure();
        }

        // Correct code. Mark verified and confirm the linked Identity contact in the same unit of
        // work so the two writes are atomic (AC-444).
        verification.MarkVerified(now);

        var user = await _identityUserService.FindByIdAsync(_userContext.UserId, ct);
        if (user is not null)
        {
            if (verification.Type == OtpVerificationType.Email)
            {
                user.EmailConfirmed = true;
            }
            else
            {
                user.PhoneNumberConfirmed = true;
            }
        }

        try
        {
            await _otpRepository.SaveChangesAsync(ct);
        }
        catch (ConcurrencyException)
        {
            // AC-442: a concurrent request already committed. Report idempotent success if it did.
            var reloaded = await _otpRepository.GetByIdAsync(request.VerificationId, ct);
            if (reloaded is not null && reloaded.IsVerified)
            {
                return _messages.Success(
                    new VerifyOtpResponse(true, reloaded.Type), ApplicationErrors.Verification.VERIFIED);
            }

            return SafeFailure();
        }

        _logger.LogInformation("OTP verified for user {UserId} ({Type})", _userContext.UserId, verification.Type);
        return _messages.Success(
            new VerifyOtpResponse(true, verification.Type), ApplicationErrors.Verification.VERIFIED);
    }

    private static bool IsSixDigits(string code) =>
        code is { Length: OtpVerification.CodeLength } && code.All(char.IsDigit);

    private Response<VerifyOtpResponse> SafeFailure() =>
        _messages.Fail<VerifyOtpResponse>(ApplicationErrors.Verification.INVALID, MessageType.Validation);
}

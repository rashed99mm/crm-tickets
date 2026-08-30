namespace CustomerSupport.Application.Features.Verification.Dtos;

/// <summary>
/// The request-OTP response: only the verification id, expiry and cooldown metadata — never the
/// code, its hash, or a routing detail (OTP-9, AC-445).
/// </summary>
public sealed record RequestOtpResponse(
    Guid VerificationId,
    DateTime ExpiresAtUtc,
    int RetryAfterSeconds,
    string Channel);
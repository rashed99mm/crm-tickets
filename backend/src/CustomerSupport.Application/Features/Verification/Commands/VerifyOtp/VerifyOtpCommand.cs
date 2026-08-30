using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Features.Verification.Dtos;
using MediatR;

namespace CustomerSupport.Application.Features.Verification.Commands.VerifyOtp;

/// <summary>
/// Verifies a pending OTP. The caller is the authenticated user (from the token); the verification
/// record's own <c>UserId</c> is what scopes the lookup, so a caller cannot verify — or even
/// discover the existence of — another user's record (AC-443).
/// </summary>
public record VerifyOtpCommand(Guid VerificationId, string Code)
    : ICommand<Response<VerifyOtpResponse>>;

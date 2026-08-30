using CustomerSupport.Domain.Entities.Verification;
using CustomerSupport.Application.Contracts;
using MediatR;

namespace CustomerSupport.Application.Features.Verification.Dtos;

/// <summary>
/// The minimal verify result. It deliberately carries no code, no hash, and no plaintext contact:
/// only whether verification succeeded and which contact type it confirmed (AC-445).
/// </summary>
public record VerifyOtpResponse(bool Verified, OtpVerificationType Type);

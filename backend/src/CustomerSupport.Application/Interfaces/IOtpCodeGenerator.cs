using CustomerSupport.Domain.Entities.Verification;

namespace CustomerSupport.Application.Interfaces;

/// <summary>
/// Generates OTP codes from a cryptographically secure source. Implemented in Infrastructure; the
/// Application layer depends only on the port so the security-relevant choice stays behind DI (A3).
/// </summary>
public interface IOtpCodeGenerator
{
    /// <summary>Produces <paramref name="length"/> uniformly distributed decimal digits (no leading-zero bias).</summary>
    string Generate(int length = OtpVerification.CodeLength);
}
using System.Security.Cryptography;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Entities.Verification;

namespace CustomerSupport.Infrastructure.Security;

/// <summary>
/// Cryptographically secure decimal digits with a uniform distribution (no modulo bias): each digit
/// is drawn from <see cref="RandomNumberGenerator"/>'s rejection-free range. The code is produced in
/// memory, handed to the dispatch request, and never stored.
/// </summary>
public sealed class OtpCodeGenerator : IOtpCodeGenerator
{
    public string Generate(int length = OtpVerification.CodeLength)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var digits = new char[length];
        for (var i = 0; i < length; i++)
        {
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        }

        return new string(digits);
    }
}
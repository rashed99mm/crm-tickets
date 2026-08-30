namespace CustomerSupport.Application.Interfaces;

/// <summary>
/// One-way hashing of OTP codes. The plaintext is never stored, logged, or returned. The verify
/// handler hashes the submitted code and compares it to the stored hash on the verification record.
/// </summary>
public interface IOtpCodeHasher
{
    string Hash(string code);
    bool Verify(string code, string hash);
}

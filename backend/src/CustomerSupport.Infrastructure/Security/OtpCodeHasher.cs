using System.Security.Cryptography;
using System.Text;
using CustomerSupport.Application.Interfaces;

namespace CustomerSupport.Infrastructure.Security;

/// <summary>
/// SHA-256 of the code plus a server-side pepper. The plaintext is never stored or returned; the
/// verify handler compares a freshly hashed submission to the stored hash using a constant-time
/// check to avoid timing side-channels.
/// </summary>
public class OtpCodeHasher : IOtpCodeHasher
{
    private readonly byte[] _pepper;

    public OtpCodeHasher(string pepper)
    {
        _pepper = Encoding.UTF8.GetBytes(pepper);
    }

    public string Hash(string code)
    {
        var codeBytes = Encoding.UTF8.GetBytes(code);
        var combined = new byte[_pepper.Length + codeBytes.Length];
        Buffer.BlockCopy(_pepper, 0, combined, 0, _pepper.Length);
        Buffer.BlockCopy(codeBytes, 0, combined, _pepper.Length, codeBytes.Length);

        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(combined));
    }

    public bool Verify(string code, string hash)
    {
        var computed = Hash(code);
        var a = Encoding.UTF8.GetBytes(computed);
        var b = Encoding.UTF8.GetBytes(hash);
        if (a.Length != b.Length)
        {
            return false;
        }

        var difference = 0;
        for (var i = 0; i < a.Length; i++)
        {
            difference |= a[i] ^ b[i];
        }

        return difference == 0;
    }
}

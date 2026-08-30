using System.Security.Cryptography;
using System.Text;
using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;

namespace CustomerSupport.Infrastructure.Channels;

/// <summary>
/// Verifies Meta's <c>X-Hub-Signature-256</c> header: HMAC-SHA256 over the exact raw body bytes,
/// keyed by the WhatsApp app secret (CC-5/CC-27). Runs against the untouched byte stream — model
/// binding may reformat whitespace, which would break the signature.
///
/// The app secret is stored in the <c>WhatsAppGateway</c> configuration's credential slot, protected
/// like every other credential, and restored here only to compute the digest.
/// </summary>
public sealed class MetaSignatureVerifier(
    IExternalApiConfigurationProvider configProvider)
    : IWebhookSignatureVerifier
{
    public bool Verify(string provider, string? signature, string? requestUrl, byte[] rawBody)
    {
        if (provider != "WhatsApp")
        {
            return false;
        }

        if (rawBody is null || rawBody.Length == 0 || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var config = configProvider.GetConfig(NotificationGatewayConstants.WhatsAppGatewayConfigName);
        if (config is null)
        {
            return false;
        }

        // The database configuration provider decrypts credential values at its boundary.
        // Decrypting here again would reject every otherwise-valid signature.
        var secret = config.Auth.Value;
        if (string.IsNullOrEmpty(secret))
        {
            return false;
        }

        var digest = Convert.ToHexString(
            new HMACSHA256(Encoding.UTF8.GetBytes(secret)).ComputeHash(rawBody)).ToLowerInvariant();
        var expected = $"sha256={digest}";
        var received = signature.Trim();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(received));
    }
}

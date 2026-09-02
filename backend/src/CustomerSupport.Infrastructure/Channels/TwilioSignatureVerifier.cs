using System.Security.Cryptography;
using System.Text;
using System.Web;
using CustomerSupport.Application.Channels;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;

namespace CustomerSupport.Infrastructure.Channels;

/// <summary>
/// Verifies Twilio's <c>X-Twilio-Signature</c> header (CC-40/CC-41): HMAC-SHA1 over the request URL
/// followed by every POST parameter's key and value concatenated in ordinal key order, Base64
/// encoded. Three things differ from <see cref="MetaSignatureVerifier"/> and all three matter —
/// SHA1 not SHA256, Base64 not hex, and URL-plus-sorted-params not the raw body. This is why
/// <see cref="IWebhookSignatureVerifier.Verify"/> carries a <c>requestUrl</c> Meta ignores.
///
/// The account auth token lives in the <c>SmsGateway</c> configuration's credential slot. As with
/// the Meta verifier, it arrives already decrypted from the database provider's boundary — a second
/// Unprotect here is the CC-51 defect, not a safety measure.
/// </summary>
public sealed class TwilioSignatureVerifier(
    IExternalApiConfigurationProvider configProvider)
    : IWebhookSignatureVerifier
{
    public bool Verify(string provider, string? signature, string? requestUrl, byte[] rawBody)
    {
        if (provider != "SMS")
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(requestUrl))
        {
            return false;
        }

        var config = configProvider.GetConfig(NotificationGatewayConstants.SmsGatewayConfigName);
        var secret = config?.Auth.Value;
        if (string.IsNullOrEmpty(secret))
        {
            return false;
        }

        var expected = Compute(secret, requestUrl, rawBody);
        var received = signature.Trim();


        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(received));
    }

    private static string Compute(string secret, string requestUrl, byte[] rawBody)
    {
        // Twilio signs decoded values, so the form has to be parsed rather than hashed as received.
        // An empty body is legal here: the URL alone is then the signed material.
        var form = HttpUtility.ParseQueryString(
            Encoding.UTF8.GetString(rawBody ?? []), Encoding.UTF8);

        var payload = new StringBuilder(requestUrl);
        foreach (var key in form.AllKeys.Where(k => k is not null).OrderBy(k => k, StringComparer.Ordinal))
        {
            payload.Append(key).Append(form[key]);
        }

        using var mac = new HMACSHA1(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(mac.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString())));
    }
}

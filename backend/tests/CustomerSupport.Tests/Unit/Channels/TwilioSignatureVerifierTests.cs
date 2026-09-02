using System.Security.Cryptography;
using System.Text;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Infrastructure.Channels;
using FluentAssertions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Channels;

/// <summary>
/// CC-40/CC-41 — Twilio's inbound signature scheme: HMAC-SHA1, Base64, over the request URL plus
/// alphabetically-ordered POST parameters. Deliberately unit-level: the algorithm is the whole risk
/// here, and it is a pure function of (url, body, secret).
/// </summary>
public class TwilioSignatureVerifierTests
{
    private const string AuthToken = "twilio-auth-token-for-tests-only";
    private const string Url = "https://support.example.com/api/channels/sms/webhook";

    private static TwilioSignatureVerifier CreateSut(string? secret = AuthToken)
    {
        var provider = new Mock<IExternalApiConfigurationProvider>();
        provider
            .Setup(p => p.GetConfig(NotificationGatewayConstants.SmsGatewayConfigName))
            .Returns(secret is null
                ? null
                : new ExternalApiConfig
                {
                    BaseUrl = "https://api.twilio.com",
                    TimeoutSeconds = 30,
                    Auth = new ExternalApiAuthConfig { Type = ExternalApiAuthType.None, Value = secret },
                });

        return new TwilioSignatureVerifier(provider.Object);
    }

    /// <summary>Twilio's documented recipe, written out independently of the implementation.</summary>
    private static string Sign(string secret, string url, params (string Key, string Value)[] form)
    {
        var payload = new StringBuilder(url);
        foreach (var (key, value) in form.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            payload.Append(key).Append(value);
        }

        using var mac = new HMACSHA1(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(mac.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString())));
    }

    private static byte[] FormBody(params (string Key, string Value)[] form) =>
        Encoding.UTF8.GetBytes(string.Join("&", form.Select(f =>
            $"{Uri.EscapeDataString(f.Key)}={Uri.EscapeDataString(f.Value)}")));

    [Fact]
    [Trait("AC", "CC40")]
    public void CC40_ValidTwilioSignature_IsAccepted()
    {
        var form = new[] { ("From", "+15559998888"), ("Body", "help me"), ("MessageSid", "SM123") };
        var signature = Sign(AuthToken, Url, form);

        CreateSut().Verify("SMS", signature, Url, FormBody(form)).Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "CC40")]
    public void CC40_ParameterOrderInTheBody_DoesNotAffectTheResult()
    {
        // Twilio sorts by key when signing; the body's own order is arbitrary. A verifier that
        // hashed the body in wire order would pass the test above and fail in production.
        var signed = new[] { ("Body", "help me"), ("From", "+15559998888") };
        var signature = Sign(AuthToken, Url, signed);
        var shuffledBody = FormBody(("From", "+15559998888"), ("Body", "help me"));

        CreateSut().Verify("SMS", signature, Url, shuffledBody).Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "CC41")]
    public void CC41_WrongSignature_IsRefused()
    {
        var form = new[] { ("From", "+15559998888"), ("Body", "forged") };
        var wrong = Sign("some-other-token", Url, form);

        CreateSut().Verify("SMS", wrong, Url, FormBody(form)).Should().BeFalse();
    }

    [Theory]
    [Trait("AC", "CC41")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CC41_MissingSignature_IsRefused(string? signature)
    {
        var form = new[] { ("From", "+15559998888"), ("Body", "unsigned") };

        CreateSut().Verify("SMS", signature, Url, FormBody(form)).Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC41")]
    public void CC41_DifferentUrl_IsRefused()
    {
        // The URL is part of the signed material, so a replay against another route fails.
        var form = new[] { ("From", "+15559998888"), ("Body", "replayed") };
        var signature = Sign(AuthToken, Url, form);

        CreateSut()
            .Verify("SMS", signature, "https://support.example.com/api/channels/email/webhook", FormBody(form))
            .Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC41")]
    public void CC41_AnotherProvidersRequest_IsNotAnswered()
    {
        // The composite (Task 2) relies on each verifier declining providers it does not own.
        var form = new[] { ("From", "+15559998888"), ("Body", "hello") };
        var signature = Sign(AuthToken, Url, form);

        CreateSut().Verify("WhatsApp", signature, Url, FormBody(form)).Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC41")]
    public void CC41_NoSmsGatewayConfigured_IsRefused()
    {
        var form = new[] { ("From", "+15559998888"), ("Body", "hello") };
        var signature = Sign(AuthToken, Url, form);

        CreateSut(secret: null).Verify("SMS", signature, Url, FormBody(form)).Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC41")]
    public void CC41_UrlEncodedValues_AreVerifiedDecoded()
    {
        // Twilio signs the decoded values but transmits them percent-encoded. Verifying the raw
        // encoded text would reject every message containing a space or a plus.
        var form = new[] { ("From", "+15559998888"), ("Body", "spaces & symbols") };
        var signature = Sign(AuthToken, Url, form);

        CreateSut().Verify("SMS", signature, Url, FormBody(form)).Should().BeTrue();
    }
}

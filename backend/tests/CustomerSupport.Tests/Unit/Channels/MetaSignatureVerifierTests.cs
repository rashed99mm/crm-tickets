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
/// FEAT-24 — Meta's X-Hub-Signature-256: HMAC-SHA256 over the exact raw body, keyed by the
/// WhatsApp app secret (CC-5/CC-27). The verifier is a pure port; the controller extracts the
/// signature header and the untouched bytes and passes the three primitives in.
/// </summary>
public class MetaSignatureVerifierTests
{
    private const string AppSecret = "super-secret-whatsapp-app-secret";

    private static readonly ExternalApiConfig Config = new()
    {
        BaseUrl = "http://127.0.0.1/messages",
        TimeoutSeconds = 30,
        Auth = new ExternalApiAuthConfig { Type = ExternalApiAuthType.Bearer, Value = AppSecret },
    };

    private readonly Mock<IExternalApiConfigurationProvider> _provider = new();

    public MetaSignatureVerifierTests()
    {
        _provider.Setup(p => p.GetConfig(NotificationGatewayConstants.WhatsAppGatewayConfigName)).Returns(Config);
    }

    private MetaSignatureVerifier CreateSut() => new(_provider.Object);

    private static string Sign(string secret, byte[] raw) =>
        "sha256=" + Convert.ToHexString(new HMACSHA256(Encoding.UTF8.GetBytes(secret)).ComputeHash(raw)).ToLowerInvariant();

    [Fact]
    public void CC5_ValidSignatureOverExactRawBody_ReturnsTrue()
    {
        var raw = Encoding.UTF8.GetBytes("{\"entry\":[{\"id\":\"1\"}]}");
        var signature = Sign(AppSecret, raw);

        var result = CreateSut().Verify("WhatsApp", signature, requestUrl: null, raw);

        result.Should().BeTrue();
    }

    [Fact]
    public void CC5_SignatureOverReformattedBody_IsRejected()
    {
        // A provider signs the exact bytes it sent; any whitespace change on our side breaks the
        // signature, which is exactly why verification must run against the untouched stream and
        // why the controller reads the raw body instead of a bound model.
        var raw = Encoding.UTF8.GetBytes("{\"entry\":[{\"id\":\"1\"}]}");
        var reformatted = Encoding.UTF8.GetBytes("{\"entry\": [{\"id\": \"1\"}]}");
        var signature = Sign(AppSecret, reformatted);

        var result = CreateSut().Verify("WhatsApp", signature, requestUrl: null, raw);

        result.Should().BeFalse();
    }

    [Fact]
    public void CC5_WrongSecret_IsRejected()
    {
        var raw = Encoding.UTF8.GetBytes("payload");
        var signature = Sign("a-different-app-secret", raw);

        var result = CreateSut().Verify("WhatsApp", signature, requestUrl: null, raw);

        result.Should().BeFalse();
    }

    [Fact]
    public void CC5_MissingSignature_IsRejected()
    {
        var result = CreateSut().Verify("WhatsApp", signature: null, requestUrl: null, rawBody: [1, 2, 3]);

        result.Should().BeFalse();
    }

    [Fact]
    public void CC5_GarbageSignature_IsRejected()
    {
        var result = CreateSut().Verify("WhatsApp", "sha256=zzz", requestUrl: null, rawBody: [1, 2, 3]);

        result.Should().BeFalse();
    }

    [Fact]
    public void CC5_OtherProviderName_IsRejected()
    {
        var raw = Encoding.UTF8.GetBytes("payload");
        var signature = Sign(AppSecret, raw);

        var result = CreateSut().Verify("SMS", signature, requestUrl: null, raw);

        result.Should().BeFalse();
    }

    [Fact]
    public void CC5_ConfigMissing_IsRejected()
    {
        _provider.Setup(p => p.GetConfig(NotificationGatewayConstants.WhatsAppGatewayConfigName)).Returns((ExternalApiConfig?)null);

        var result = CreateSut().Verify("WhatsApp", "sha256=anything", requestUrl: null, rawBody: [1, 2, 3]);

        result.Should().BeFalse();
    }
}

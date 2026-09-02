using CustomerSupport.Application.Channels;
using CustomerSupport.Infrastructure.Channels;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Unit.Channels;

/// <summary>
/// CC-40/CC-41 — two providers, one interface. Each verifier declines providers it does not own
/// (asserted in TwilioSignatureVerifierTests and by MetaSignatureVerifier's own provider gate), so
/// the composite accepts a delivery when any member accepts it.
/// </summary>
public class CompositeWebhookSignatureVerifierTests
{
    private sealed class StubVerifier(string provider, bool result) : IWebhookSignatureVerifier
    {
        public int Calls { get; private set; }

        public bool Verify(string p, string? signature, string? requestUrl, byte[] rawBody)
        {
            Calls++;
            return p == provider && result;
        }
    }

    [Fact]
    [Trait("AC", "CC40")]
    public void CC40_DelegatesToTheVerifierThatOwnsTheProvider()
    {
        var meta = new StubVerifier("WhatsApp", result: true);
        var twilio = new StubVerifier("SMS", result: true);
        var sut = new CompositeWebhookSignatureVerifier([meta, twilio]);

        sut.Verify("SMS", "sig", "https://x/y", [1]).Should().BeTrue();
        sut.Verify("WhatsApp", "sig", null, [1]).Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "CC41")]
    public void CC41_WhenTheOwningVerifierRefuses_TheCompositeRefuses()
    {
        var twilio = new StubVerifier("SMS", result: false);
        var sut = new CompositeWebhookSignatureVerifier([twilio]);

        sut.Verify("SMS", "bad-sig", "https://x/y", [1]).Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC41")]
    public void CC41_AnUnknownProvider_IsRefused()
    {
        var sut = new CompositeWebhookSignatureVerifier(
            [new StubVerifier("WhatsApp", result: true), new StubVerifier("SMS", result: true)]);

        sut.Verify("Telegram", "sig", "https://x/y", [1]).Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "CC41")]
    public void CC41_NoVerifiersRegistered_IsRefusedRatherThanThrowing()
    {
        new CompositeWebhookSignatureVerifier([])
            .Verify("SMS", "sig", "https://x/y", [1]).Should().BeFalse();
    }
}

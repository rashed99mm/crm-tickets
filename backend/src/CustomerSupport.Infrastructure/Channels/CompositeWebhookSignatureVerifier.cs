using CustomerSupport.Application.Channels;

namespace CustomerSupport.Infrastructure.Channels;

/// <summary>
/// The single <see cref="IWebhookSignatureVerifier"/> the host registers once more than one provider
/// posts webhooks (CC-40/CC-41, spec A22). Each member verifier gates on the provider it owns and
/// returns false for the rest, so "any member accepts it" is the same answer as "the owning member
/// accepts it" — with no provider-name table to keep in step with the registrations.
///
/// A delivery no member owns is refused, which is the safe default: an unrecognised provider is
/// exactly the shape of an attacker probing for an unguarded webhook.
/// </summary>
public sealed class CompositeWebhookSignatureVerifier(
    IEnumerable<IWebhookSignatureVerifier> verifiers)
    : IWebhookSignatureVerifier
{
    private readonly IReadOnlyList<IWebhookSignatureVerifier> _verifiers = verifiers.ToList();

    public bool Verify(string provider, string? signature, string? requestUrl, byte[] rawBody) =>
        _verifiers.Any(v => v.Verify(provider, signature, requestUrl, rawBody));
}

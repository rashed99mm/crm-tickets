using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Channels.Commands.IngestInboundEmail;

/// <summary>
/// SendGrid Inbound Parse's posted fields, as far as this channel reads them (CC-42/CC-43).
///
/// Bound with <c>[FromForm]</c> from the provider's <c>multipart/form-data</c>; the names are
/// SendGrid's own, and model binding matches them case-insensitively.
///
/// <c>Headers</c> is the original message's raw header block, forwarded verbatim. It is the only
/// place a stable per-message id exists — Inbound Parse has no id field of its own — which is why
/// it is carried rather than dropped.
/// </summary>
public record SendGridInboundEmailRequest(
    string? From,
    string? Subject,
    string? Text,
    string? Headers,
    string? Envelope);

/// <summary>
/// CC-42/CC-43 — one inbound email, still in the provider's own vocabulary. The handler is what
/// turns a <c>From</c> header into an address and a display name, and a raw header block into a
/// provider message id, before the shared ingestion path sees any of it.
/// </summary>
public record IngestInboundEmailCommand(
    string? From,
    string? Subject,
    string? Text,
    string? Headers) : ICommand<Response<Guid>>;

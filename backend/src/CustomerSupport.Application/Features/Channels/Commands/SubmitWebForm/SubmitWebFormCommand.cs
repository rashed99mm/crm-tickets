using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Channels.Commands.SubmitWebForm;

/// <summary>
/// The portal web form's request body, field-for-field as the portal already sends it
/// (<c>frontend/projects/common/src/lib/channels/web-form.api.ts</c>, spec A20).
///
/// Lives beside its command rather than inside the controller, following
/// <c>RecordTicketMessageRequest</c>: the shape is part of the use case, and a controller that
/// declares its own contracts cannot be read without reading the controller.
///
/// <c>Honeypot</c> is optional and must stay optional — the portal sends it only when its hidden
/// input was filled, and a bot posting directly may not send it at all.
/// </summary>
public record WebFormSubmissionRequest(
    string Name,
    string Email,
    string Subject,
    string Description,
    string? Honeypot);

/// <summary>
/// What the customer is shown: the reference to quote back to support. Carried inside
/// <c>Response&lt;T&gt;.Data</c>, which portal-app's envelope interceptor unwraps — so this is
/// exactly the object the browser receives, including the nested <c>Success</c> its declared
/// TypeScript interface expects.
/// </summary>
public record WebFormSubmissionResult(string Reference, bool Success);

/// <summary>
/// CC-20..CC-23 / CC-47 — an anonymous visitor's web-form submission.
///
/// <c>ClientKey</c> is supplied by the caller because rate limiting is per client and the
/// Application layer has no <c>HttpContext</c> to read a remote address from. It must never come
/// from the payload, which an attacker chooses.
/// </summary>
public record SubmitWebFormCommand(
    string Name,
    string Email,
    string Subject,
    string Description,
    string? Honeypot,
    string ClientKey) : ICommand<Response<WebFormSubmissionResult>>;

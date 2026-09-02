using CustomerSupport.Application.Contracts;

namespace CustomerSupport.Application.Features.Channels.Commands.IngestInboundChannelMessage;

/// <summary>
/// CC-1..CC-4. Normalizes an inbound external-channel message (WhatsApp, SMS, web form) into a
/// customer + open ticket + appended message, before any provider-specific controller has touched
/// the payload. Anonymous by nature — there is no <c>IUserContext</c> on this path.
/// </summary>
public record IngestInboundChannelMessageCommand(
    string Channel,
    string? CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    string Body,
    string? ProviderMessageId,
    /// <summary>
    /// A23 — the subject for a newly-created ticket, when the channel actually carries one: the web
    /// form collects it and an email has a Subject: header. Null for WhatsApp and SMS, which have no
    /// subject concept, and the handler then synthesizes its "{Channel} — {Name}" default as before.
    /// Last, with a default, so existing call sites are untouched.
    /// </summary>
    string? Subject = null) : ICommand<Response<Guid>>;
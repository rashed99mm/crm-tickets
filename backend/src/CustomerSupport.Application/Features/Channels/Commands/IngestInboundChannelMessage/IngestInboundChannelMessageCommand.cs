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
    string? ProviderMessageId) : ICommand<Response<Guid>>;
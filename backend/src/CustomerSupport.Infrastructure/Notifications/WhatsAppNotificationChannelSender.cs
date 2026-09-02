using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.ValueObjects;
using CustomerSupport.Infrastructure.Notifications.Channels;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CustomerSupport.Infrastructure.Notifications;

/// <summary>
/// Delivers outbound ticket replies through the configured <c>WhatsAppGateway</c> integration URL
/// using the WhatsApp Cloud API message shape (Meta Graph <c>POST /{phone-number-id}/messages</c>).
/// CC-6/CC-7 — same bounded retry contract as the email and SMS senders (<see cref="ChannelHttpSender"/>).
/// </summary>
public sealed class WhatsAppNotificationChannelSender : ChannelHttpSender
{
    public WhatsAppNotificationChannelSender(
        IExternalApiConfigurationProvider configProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<WhatsAppNotificationChannelSender> logger)
        : base(configProvider, httpClientFactory, logger)
    {
    }

    public override NotificationChannel SupportedChannel => NotificationChannel.WhatsApp;

    protected override string ConfigName => NotificationGatewayConstants.WhatsAppGatewayConfigName;

    protected override HttpContent BuildContent(RenderedNotification notification) =>
        JsonContent(new
        {
            messaging_product = "whatsapp",
            to = notification.PhoneNumber,
            type = "text",
            text = new { body = notification.Message },
        });

    protected override async Task<string?> ReadProviderMessageIdAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            return document.RootElement.TryGetProperty("messages", out var messages)
                && messages.ValueKind == JsonValueKind.Array
                && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("id", out var id)
                    ? id.GetString()
                    : null;
        }
        catch (JsonException)
        {
            // A 2xx with a body we cannot parse is still a successful send; the id is simply
            // unknown. Never fabricate one (CC-49).
            return null;
        }
    }
}

using CustomerSupport.Application.Common.Options;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.ValueObjects;
using CustomerSupport.Infrastructure.Notifications.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CustomerSupport.Infrastructure.Notifications;

/// <summary>
/// Delivers SMS through the configured <c>SmsGateway</c> integration URL, speaking Twilio's
/// contract (CC-36). See <see cref="ChannelHttpSender"/> for the shared transport/retry/auth
/// contract.
/// </summary>
public sealed class SmsNotificationChannelSender : ChannelHttpSender
{
    private readonly ChannelOptions _options;

    public SmsNotificationChannelSender(
        IExternalApiConfigurationProvider configProvider,
        IHttpClientFactory httpClientFactory,
        IOptions<ChannelOptions> options,
        ILogger<SmsNotificationChannelSender> logger)
        : base(configProvider, httpClientFactory, logger)
    {
        _options = options.Value;
    }

    public override NotificationChannel SupportedChannel => NotificationChannel.Sms;

    protected override string ConfigName => NotificationGatewayConstants.SmsGatewayConfigName;

    /// <summary>Twilio's `POST /2010-04-01/Accounts/{sid}/Messages.json` takes form encoding, not
    /// JSON — the one channel here that is not a JSON API.</summary>
    protected override HttpContent BuildContent(RenderedNotification notification) =>
        new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = notification.PhoneNumber ?? string.Empty,
            ["From"] = _options.SmsFrom,
            ["Body"] = notification.Message,
        });

    protected override Task<string?> ReadProviderMessageIdAsync(
        HttpResponseMessage response, CancellationToken ct) =>
        ReadJsonStringAsync(response, "sid", ct);
}

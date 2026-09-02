using CustomerSupport.Application.Common.Options;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.ValueObjects;
using CustomerSupport.Infrastructure.Notifications.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;

namespace CustomerSupport.Infrastructure.Notifications;

/// <summary>
/// Delivers email through the configured <c>EmailGateway</c> integration URL, speaking SendGrid
/// v3's contract (CC-34). See <see cref="ChannelHttpSender"/> for the shared transport/retry/auth
/// contract.
/// </summary>
public sealed class EmailNotificationChannelSender : ChannelHttpSender
{
    private readonly ChannelOptions _options;

    public EmailNotificationChannelSender(
        IExternalApiConfigurationProvider configProvider,
        IHttpClientFactory httpClientFactory,
        IOptions<ChannelOptions> options,
        ILogger<EmailNotificationChannelSender> logger)
        : base(configProvider, httpClientFactory, logger)
    {
        _options = options.Value;
    }

    public override NotificationChannel SupportedChannel => NotificationChannel.Email;

    protected override string ConfigName => NotificationGatewayConstants.EmailGatewayConfigName;

    /// <summary>SendGrid v3 `POST /v3/mail/send`. `from` is required and had no equivalent in the
    /// house payload this replaces, so it comes from `Channels:EmailFrom`.</summary>
    protected override HttpContent BuildContent(RenderedNotification notification) =>
        JsonContent(new
        {
            personalizations = new[]
            {
                new { to = new[] { new { email = notification.Email } } },
            },
            from = new { email = _options.EmailFrom },
            subject = notification.Title,
            content = new[]
            {
                new { type = "text/plain", value = notification.Message },
            },
        });

    /// <summary>SendGrid returns 202 with an empty body; the id is in `X-Message-Id`.</summary>
    protected override Task<string?> ReadProviderMessageIdAsync(
        HttpResponseMessage response, CancellationToken ct) =>
        Task.FromResult(response.Headers.TryGetValues("X-Message-Id", out var values)
            ? values.FirstOrDefault()
            : null);
}

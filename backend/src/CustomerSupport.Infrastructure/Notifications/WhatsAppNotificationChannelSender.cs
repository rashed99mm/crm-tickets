using CustomerSupport.Application.Errors;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CustomerSupport.Infrastructure.Notifications;

/// <summary>
/// Delivers outbound ticket replies through the configured <c>WhatsAppGateway</c> integration URL
/// using the WhatsApp Cloud API message shape (Meta Graph <c>POST /{phone-number-id}/messages</c>).
/// Credentials are restored only at the transport boundary via <see cref="ISecretProtector"/> and
/// never logged. CC-6/CC-7 — same bounded retry contract as the email and SMS senders.
/// </summary>
public sealed class WhatsAppNotificationChannelSender : INotificationChannelSender
{
    private readonly IExternalApiConfigurationProvider _configProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecretProtector _secretProtector;
    private readonly ILogger<WhatsAppNotificationChannelSender> _logger;

    public WhatsAppNotificationChannelSender(
        IExternalApiConfigurationProvider configProvider,
        IHttpClientFactory httpClientFactory,
        ISecretProtector secretProtector,
        ILogger<WhatsAppNotificationChannelSender> logger)
    {
        _configProvider = configProvider;
        _httpClientFactory = httpClientFactory;
        _secretProtector = secretProtector;
        _logger = logger;
    }

    public NotificationChannel SupportedChannel => NotificationChannel.WhatsApp;

    public async Task<ChannelSendResult> SendAsync(RenderedNotification notification, CancellationToken ct = default)
    {
        var config = _configProvider.GetConfig(NotificationGatewayConstants.WhatsAppGatewayConfigName);
        if (config is null)
        {
            _logger.LogWarning("WhatsApp gateway configuration '{Config}' is missing", NotificationGatewayConstants.WhatsAppGatewayConfigName);
            return new ChannelSendResult(NotificationChannel.WhatsApp, false, ApplicationErrors.Notification.CONFIG_MISSING);
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            to = notification.PhoneNumber,
            type = "text",
            text = new { body = notification.Message },
        };
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, config.TimeoutSeconds));
        ApplyAuth(client, config.Auth);

        for (var attempt = 1; attempt <= NotificationGatewayConstants.TransientRetryCount; attempt++)
        {
            try
            {
                using var response = await client.PostAsync(config.BaseUrl, content, ct);
                if (response.IsSuccessStatusCode)
                    return new ChannelSendResult(NotificationChannel.WhatsApp, true, ProviderMessageId: $"wa:{Guid.NewGuid():N}");

                if (!IsTransient(response.StatusCode))
                {
                    _logger.LogWarning("WhatsApp gateway returned {StatusCode} (non-transient)", (int)response.StatusCode);
                    return new ChannelSendResult(NotificationChannel.WhatsApp, false, ApplicationErrors.Notification.DELIVERY_FAILED);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                _logger.LogWarning(ex, "WhatsApp gateway transient failure on attempt {Attempt}", attempt);
            }

            if (attempt < NotificationGatewayConstants.TransientRetryCount)
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct);
        }

        return new ChannelSendResult(NotificationChannel.WhatsApp, false, ApplicationErrors.Notification.DELIVERY_FAILED);
    }

    private void ApplyAuth(HttpClient client, ExternalApiAuthConfig auth)
    {
        switch (auth.Type)
        {
            case ExternalApiAuthType.ApiKey:
                client.DefaultRequestHeaders.Remove(auth.KeyName);
                client.DefaultRequestHeaders.Add(auth.KeyName, _secretProtector.Unprotect(auth.Value));
                break;
            case ExternalApiAuthType.Bearer:
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _secretProtector.Unprotect(auth.Token));
                break;
            case ExternalApiAuthType.Basic:
                var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{auth.ClientId}:{auth.ClientSecret}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
                break;
            case ExternalApiAuthType.OAuth2:
            case ExternalApiAuthType.None:
            default:
                break;
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.RequestTimeout;

    private static bool IsTransient(Exception ex) =>
        ex is TimeoutException or HttpRequestException or OperationCanceledException;
}
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
/// Delivers email through the configured <c>EmailGateway</c> integration URL. Credentials are
/// restored only at the transport boundary via <see cref="ISecretProtector"/> and never logged.
/// </summary>
public sealed class EmailNotificationChannelSender : INotificationChannelSender
{
    private readonly IExternalApiConfigurationProvider _configProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecretProtector _secretProtector;
    private readonly ILogger<EmailNotificationChannelSender> _logger;

    public EmailNotificationChannelSender(
        IExternalApiConfigurationProvider configProvider,
        IHttpClientFactory httpClientFactory,
        ISecretProtector secretProtector,
        ILogger<EmailNotificationChannelSender> logger)
    {
        _configProvider = configProvider;
        _httpClientFactory = httpClientFactory;
        _secretProtector = secretProtector;
        _logger = logger;
    }

    public NotificationChannel SupportedChannel => NotificationChannel.Email;

    public async Task<ChannelSendResult> SendAsync(RenderedNotification notification, CancellationToken ct = default)
    {
        var config = _configProvider.GetConfig(NotificationGatewayConstants.EmailGatewayConfigName);
        if (config is null)
        {
            _logger.LogWarning("Email gateway configuration '{Config}' is missing", NotificationGatewayConstants.EmailGatewayConfigName);
            return new ChannelSendResult(NotificationChannel.Email, false, ApplicationErrors.Notification.CONFIG_MISSING);
        }

        var payload = new { to = notification.Email, subject = notification.Title, body = notification.Message };
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
                    return new ChannelSendResult(NotificationChannel.Email, true, ProviderMessageId: $"email:{Guid.NewGuid():N}");

                if (!IsTransient(response.StatusCode))
                {
                    _logger.LogWarning("Email gateway returned {StatusCode} (non-transient)", (int)response.StatusCode);
                    return new ChannelSendResult(NotificationChannel.Email, false, ApplicationErrors.Notification.DELIVERY_FAILED);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                _logger.LogWarning(ex, "Email gateway transient failure on attempt {Attempt}", attempt);
            }

            if (attempt < NotificationGatewayConstants.TransientRetryCount)
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct);
        }

        return new ChannelSendResult(NotificationChannel.Email, false, ApplicationErrors.Notification.DELIVERY_FAILED);
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

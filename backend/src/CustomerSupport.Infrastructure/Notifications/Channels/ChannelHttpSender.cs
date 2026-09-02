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

namespace CustomerSupport.Infrastructure.Notifications.Channels;

/// <summary>
/// The transport half of every HTTP channel sender: configuration lookup, client construction,
/// auth, the bounded retry policy (NG-3/NG-4) and result mapping. A subclass supplies only its
/// provider's payload and how to read a message id out of the response (CC-49).
///
/// The credential arrives already decrypted from IExternalApiConfigurationProvider — see CC-51.
/// </summary>
public abstract class ChannelHttpSender : INotificationChannelSender
{
    private readonly IExternalApiConfigurationProvider _configProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    protected ChannelHttpSender(
        IExternalApiConfigurationProvider configProvider,
        IHttpClientFactory httpClientFactory,
        ILogger logger)
    {
        _configProvider = configProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public abstract NotificationChannel SupportedChannel { get; }

    /// <summary>The `ExternalApiConfiguration` name this channel reads, e.g. `WhatsAppGateway`.</summary>
    protected abstract string ConfigName { get; }

    /// <summary>
    /// Built fresh per attempt — a consumed HttpContent cannot be re-posted, which silently made
    /// the old retry loop send an empty body.
    /// </summary>
    protected abstract HttpContent BuildContent(RenderedNotification notification);

    /// <summary>
    /// The provider's own id for the accepted message. Null when the provider gave none; never a
    /// fabricated value, because a fabricated id cannot be reconciled against a provider dashboard
    /// and defeats the (Channel, ProviderMessageId) idempotency index.
    /// </summary>
    protected abstract Task<string?> ReadProviderMessageIdAsync(
        HttpResponseMessage response, CancellationToken ct);

    public async Task<ChannelSendResult> SendAsync(
        RenderedNotification notification, CancellationToken ct = default)
    {
        var channel = SupportedChannel;

        var config = _configProvider.GetConfig(ConfigName);
        if (config is null)
        {
            _logger.LogWarning("Channel gateway configuration '{Config}' is missing", ConfigName);
            return new ChannelSendResult(channel, false, ApplicationErrors.Notification.CONFIG_MISSING);
        }

        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, config.TimeoutSeconds));
        ApplyAuth(client, config.Auth);

        for (var attempt = 1; attempt <= NotificationGatewayConstants.TransientRetryCount; attempt++)
        {
            try
            {
                using var content = BuildContent(notification);
                using var response = await client.PostAsync(config.BaseUrl, content, ct);

                if (response.IsSuccessStatusCode)
                {
                    var providerId = await ReadProviderMessageIdAsync(response, ct);
                    return new ChannelSendResult(channel, true, ProviderMessageId: providerId);
                }

                if (!IsTransient(response.StatusCode))
                {
                    _logger.LogWarning(
                        "{Channel} gateway returned {StatusCode} (non-transient)",
                        channel.Value, (int)response.StatusCode);
                    return new ChannelSendResult(channel, false, ApplicationErrors.Notification.DELIVERY_FAILED);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                _logger.LogWarning(ex, "{Channel} gateway transient failure on attempt {Attempt}",
                    channel.Value, attempt);
            }

            if (attempt < NotificationGatewayConstants.TransientRetryCount)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct);
            }
        }

        return new ChannelSendResult(channel, false, ApplicationErrors.Notification.DELIVERY_FAILED);
    }

    protected static HttpContent JsonContent(object payload) =>
        new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    /// <summary>Reads a top-level string property out of a JSON response body, or null.</summary>
    protected static async Task<string?> ReadJsonStringAsync(
        HttpResponseMessage response, string propertyName, CancellationToken ct)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return document.RootElement.TryGetProperty(propertyName, out var value)
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void ApplyAuth(HttpClient client, ExternalApiAuthConfig auth)
    {
        // Already decrypted by the configuration provider (CC-51).
        switch (auth.Type)
        {
            case ExternalApiAuthType.ApiKey when !string.IsNullOrWhiteSpace(auth.Value):
                client.DefaultRequestHeaders.Remove(auth.KeyName);
                client.DefaultRequestHeaders.Add(auth.KeyName, auth.Value);
                break;
            case ExternalApiAuthType.Bearer when !string.IsNullOrWhiteSpace(auth.Token):
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", auth.Token);
                break;
            case ExternalApiAuthType.Basic:
                var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{auth.ClientId}:{auth.ClientSecret}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
                break;
            default:
                break;
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is >= HttpStatusCode.InternalServerError
            or HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests;

    private static bool IsTransient(Exception ex) =>
        ex is TimeoutException or HttpRequestException or OperationCanceledException;
}

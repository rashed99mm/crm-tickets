using System.Net;
using System.Text;
using System.Text.Json;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.ValueObjects;
using CustomerSupport.Infrastructure.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Notifications;

/// <summary>
/// FEAT-24 — the WhatsApp outbound sender (CC-6/CC-7). Same retry/secret contract as the email
/// and SMS senders (NG-2/NG-3/NG-4); the base URL is always a test sandbox, never a live Meta
/// endpoint (spec A11).
/// </summary>
public class WhatsAppNotificationChannelSenderTests
{
    private const string ApiBaseUrl = "http://sandbox.whatsapp.test/v19.0/112233445/messages";

    private readonly Mock<IExternalApiConfigurationProvider> _configProvider = new();

    private readonly RecordingHttpMessageHandler _recorder = new();

    public WhatsAppNotificationChannelSenderTests()
    {
        _configProvider
            .Setup(p => p.GetConfig(NotificationGatewayConstants.WhatsAppGatewayConfigName))
            .Returns(new ExternalApiConfig
            {
                BaseUrl = ApiBaseUrl,
                TimeoutSeconds = 30,
                Auth = new ExternalApiAuthConfig
                {
                    Type = ExternalApiAuthType.Bearer,
                    Token = "sandbox-token-not-a-live-credential",
                },
            });
    }

    private static RenderedNotification Notification(string phone = "15559998888", string body = "Your bill is ready.") =>
        new(
            RecipientUserId: null,
            Email: null,
            PhoneNumber: phone,
            Title: "Ticket reply",
            Message: body,
            NotificationType: "TICKET_REPLY",
            Channel: NotificationChannel.WhatsApp,
            Locale: null);

    private WhatsAppNotificationChannelSender CreateSut() =>
        new(
            _configProvider.Object,
            new FakeHttpClientFactory(_recorder.Client),
            new IdentitySecretProtector(),
            NullLogger<WhatsAppNotificationChannelSender>.Instance);

    [Fact]
    public void CC6_SupportedChannel_IsWhatsApp()
    {
        CreateSut().SupportedChannel.Should().Be(NotificationChannel.WhatsApp);
    }

    [Fact]
    public async Task CC6_SendAsync_CallsTheConfiguredUrlWithTheWhatsAppCloudApiPayloadShape()
    {
        _recorder.Queue(HttpStatusCode.OK);

        var result = await CreateSut().SendAsync(Notification("15559998888", "Your bill is ready."));

        result.Succeeded.Should().BeTrue();
        result.ErrorCode.Should().BeNull();

        var calls = _recorder.Calls;
        calls.Should().HaveCount(1);
        calls[0].Url.Should().Be(ApiBaseUrl);

        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(calls[0].Body));
        json.RootElement.GetProperty("messaging_product").GetString().Should().Be("whatsapp");
        json.RootElement.GetProperty("to").GetString().Should().Be("15559998888");
        json.RootElement.GetProperty("type").GetString().Should().Be("text");
        json.RootElement.GetProperty("text").GetProperty("body").GetString().Should().Be("Your bill is ready.");
    }

    [Fact]
    public async Task CC6_SendAsync_RestoresTheBearerCredentialOnlyAtTheTransportBoundary()
    {
        _recorder.Queue(HttpStatusCode.OK);

        await CreateSut().SendAsync(Notification());

        _recorder.Calls.Single().Authorization.Should().Be("Bearer sandbox-token-not-a-live-credential");
    }

    [Fact]
    public async Task CC7_TransientFailure_RetriesUpToTheBoundedCount()
    {
        _recorder.Queue(HttpStatusCode.InternalServerError);
        _recorder.Queue(HttpStatusCode.InternalServerError);
        _recorder.Queue(HttpStatusCode.InternalServerError);

        var result = await CreateSut().SendAsync(Notification());

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ApplicationErrors.Notification.DELIVERY_FAILED);
        _recorder.Calls.Should().HaveCount(NotificationGatewayConstants.TransientRetryCount);
    }

    [Fact]
    public async Task CC7_TransientThenSuccess_RecoversWithinTheRetryBudget()
    {
        _recorder.Queue(HttpStatusCode.InternalServerError);
        _recorder.Queue(HttpStatusCode.ServiceUnavailable);
        _recorder.Queue(HttpStatusCode.OK);

        var result = await CreateSut().SendAsync(Notification());

        result.Succeeded.Should().BeTrue();
        _recorder.Calls.Should().HaveCount(3);
    }

    [Fact]
    public async Task CC7_PermanentFailure_IsNeverRetried()
    {
        _recorder.Queue(HttpStatusCode.BadRequest);

        var result = await CreateSut().SendAsync(Notification());

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ApplicationErrors.Notification.DELIVERY_FAILED);
        _recorder.Calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task CC6_ConfigMissing_ReturnsConfigMissingWithoutCallingAnything()
    {
        _configProvider
            .Setup(p => p.GetConfig(NotificationGatewayConstants.WhatsAppGatewayConfigName))
            .Returns((ExternalApiConfig?)null);

        var result = await CreateSut().SendAsync(Notification());

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ApplicationErrors.Notification.CONFIG_MISSING);
        _recorder.Calls.Should().BeEmpty();
    }
}

/// <summary>A raw HttpClient whose every request is recorded instead of leaving the process.</summary>
public sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpStatusCode> _responses = new();
    private readonly List<(string Url, byte[] Body, string? Authorization)> _calls = new();

    public IReadOnlyList<(string Url, byte[] Body, string? Authorization)> Calls
    {
        get
        {
            lock (_calls)
            {
                return _calls.ToList();
            }
        }
    }

    public HttpClient Client { get; }

    public RecordingHttpMessageHandler()
    {
        Client = new HttpClient(this);
    }

    public void Queue(HttpStatusCode status) => _responses.Enqueue(status);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var status = _responses.Count > 0 ? _responses.Dequeue() : HttpStatusCode.OK;

        var body = request.Content is null
            ? Array.Empty<byte>()
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);

        lock (_calls)
        {
            _calls.Add((request.RequestUri?.ToString() ?? string.Empty, body,
                request.Headers.Authorization?.ToString()));
        }

        return new HttpResponseMessage(status);
    }
}

/// <summary>Identity-only protector for unit tests; the DPAPI-backed implementation is
/// exercised end-to-end by the integration webhook tests.</summary>
public sealed class IdentitySecretProtector : ISecretProtector
{
    public string Protect(string value) => value;
    public string Unprotect(string protectedValue) => protectedValue;
}

/// <summary>Returns a single client wired to the recording handler — the same handoff the real
/// <c>AddHttpClient</c>() registration gives the sender.</summary>
public sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}
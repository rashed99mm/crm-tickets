using System.Linq;
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
    public async Task CC51_PlaintextCredentialFromTheProvider_ReachesTheAuthorizationHeader()
    {
        // The provider decrypts before handing the config over (DatabaseExternalApiProvider.MapToConfig),
        // so a sender must treat Auth.Token as plaintext and never unprotect it a second time.
        _configProvider
            .Setup(p => p.GetConfig(NotificationGatewayConstants.WhatsAppGatewayConfigName))
            .Returns(new ExternalApiConfig
            {
                BaseUrl = ApiBaseUrl,
                TimeoutSeconds = 30,
                Auth = new ExternalApiAuthConfig
                {
                    Type = ExternalApiAuthType.Bearer,
                    Token = "already-decrypted-token",
                },
            });
        _recorder.Queue(HttpStatusCode.OK);

        var result = await CreateSut().SendAsync(Notification());

        result.Succeeded.Should().BeTrue();
        _recorder.Calls.Should().HaveCount(1);
        _recorder.Calls[0].Authorization.Should().Be("Bearer already-decrypted-token");
    }

    [Fact]
    public async Task CC49_ContentIsRebuiltPerAttempt_SoARetryPostsTheSameBodyAgain()
    {
        // The pre-refactor senders reused one StringContent across attempts; its stream is consumed by
        // the first PostAsync, so the retry sent nothing.
        _recorder.Queue(HttpStatusCode.ServiceUnavailable);
        _recorder.Queue(HttpStatusCode.OK);

        var result = await CreateSut().SendAsync(Notification("15559998888", "retry me"));

        result.Succeeded.Should().BeTrue();
        _recorder.Calls.Should().HaveCount(2);
        Encoding.UTF8.GetString(_recorder.Calls[0].Body).Should().Contain("retry me");
        Encoding.UTF8.GetString(_recorder.Calls[1].Body).Should()
            .Be(Encoding.UTF8.GetString(_recorder.Calls[0].Body));
    }

    [Fact]
    public async Task CC35_ProviderMessageId_IsReadFromMetasResponse()
    {
        _recorder.Queue(
            HttpStatusCode.OK,
            body: """
            {
              "messaging_product": "whatsapp",
              "contacts": [ { "input": "15559998888", "wa_id": "15559998888" } ],
              "messages": [ { "id": "wamid.HBgLMTU1NTk5OTg4ODgVAgARGBI5QTND" } ]
            }
            """);

        var result = await CreateSut().SendAsync(Notification());

        result.Succeeded.Should().BeTrue();
        result.ProviderMessageId.Should().Be("wamid.HBgLMTU1NTk5OTg4ODgVAgARGBI5QTND");
    }

    [Fact]
    public async Task CC39_PayloadIsExactlyMetaCloudApisShape()
    {
        _recorder.Queue(HttpStatusCode.OK, body: """{"messages":[{"id":"wamid.X"}]}""");

        await CreateSut().SendAsync(Notification("15559998888", "Your bill is ready."));

        using var document = JsonDocument.Parse(_recorder.Calls[0].Body);
        var root = document.RootElement;
        root.GetProperty("messaging_product").GetString().Should().Be("whatsapp");
        root.GetProperty("to").GetString().Should().Be("15559998888");
        root.GetProperty("type").GetString().Should().Be("text");
        root.GetProperty("text").GetProperty("body").GetString().Should().Be("Your bill is ready.");
        root.EnumerateObject().Select(p => p.Name).Should()
            .BeEquivalentTo(["messaging_product", "to", "type", "text"]);
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
    private readonly Queue<(HttpStatusCode Status, string? Body, IEnumerable<(string Name, string Value)>? Headers)> _responses = new();
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

    public void Queue(
        HttpStatusCode status,
        string? body = null,
        IEnumerable<(string Name, string Value)>? headers = null) => _responses.Enqueue((status, body, headers));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var queued = _responses.Count > 0
            ? _responses.Dequeue()
            : (HttpStatusCode.OK, (string?)null, (IEnumerable<(string Name, string Value)>?)null);

        var body = request.Content is null
            ? Array.Empty<byte>()
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);

        lock (_calls)
        {
            _calls.Add((request.RequestUri?.ToString() ?? string.Empty, body,
                request.Headers.Authorization?.ToString()));
        }

        var response = new HttpResponseMessage(queued.Item1);
        if (queued.Item2 is not null)
        {
            response.Content = new StringContent(queued.Item2, Encoding.UTF8, "application/json");
        }

        foreach (var (name, value) in queued.Item3 ?? [])
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }

        return response;
    }
}

/// <summary>Returns a single client wired to the recording handler — the same handoff the real
/// <c>AddHttpClient</c>() registration gives the sender.</summary>
public sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}
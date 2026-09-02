using System.Net;
using System.Text.Json;
using CustomerSupport.Application.Common.Options;
using CustomerSupport.Application.Errors;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.ValueObjects;
using CustomerSupport.Infrastructure.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CustomerSupport.Tests.Unit.Notifications;

public class EmailNotificationChannelSenderTests
{
    private const string ApiBaseUrl = "http://localhost:3001/mock/sendgrid/v3/mail/send";

    private readonly Mock<IExternalApiConfigurationProvider> _configProvider = new();
    private readonly RecordingHttpMessageHandler _recorder = new();

    public EmailNotificationChannelSenderTests()
    {
        _configProvider
            .Setup(p => p.GetConfig(NotificationGatewayConstants.EmailGatewayConfigName))
            .Returns(new ExternalApiConfig { BaseUrl = ApiBaseUrl, TimeoutSeconds = 30 });
    }

    private EmailNotificationChannelSender CreateSut() =>
        new(
            _configProvider.Object,
            new FakeHttpClientFactory(_recorder.Client),
            Options.Create(new ChannelOptions { EmailFrom = "support@commandcenter.local" }),
            NullLogger<EmailNotificationChannelSender>.Instance);

    private static RenderedNotification Notification() =>
        new(null, "customer@example.com", null, "Ticket TKT-001001 updated",
            "Your ticket moved to Resolved.", "TICKET_REPLY", NotificationChannel.Email, null);

    [Fact]
    public async Task CC39_PayloadIsExactlySendGridV3sShape()
    {
        _recorder.Queue(HttpStatusCode.Accepted, headers: [("X-Message-Id", "sg-abc123")]);

        await CreateSut().SendAsync(Notification());

        using var document = JsonDocument.Parse(_recorder.Calls[0].Body);
        var root = document.RootElement;

        root.GetProperty("personalizations")[0].GetProperty("to")[0]
            .GetProperty("email").GetString().Should().Be("customer@example.com");
        root.GetProperty("from").GetProperty("email").GetString().Should().Be("support@commandcenter.local");
        root.GetProperty("subject").GetString().Should().Be("Ticket TKT-001001 updated");
        root.GetProperty("content")[0].GetProperty("type").GetString().Should().Be("text/plain");
        root.GetProperty("content")[0].GetProperty("value").GetString()
            .Should().Be("Your ticket moved to Resolved.");
    }

    [Fact]
    public async Task CC34_MessageIdComesFromTheXMessageIdHeaderOfAn202WithNoBody()
    {
        // SendGrid answers 202 Accepted with an empty body; the id is a header.
        _recorder.Queue(HttpStatusCode.Accepted, headers: [("X-Message-Id", "sg-abc123")]);

        var result = await CreateSut().SendAsync(Notification());

        result.Succeeded.Should().BeTrue();
        result.ProviderMessageId.Should().Be("sg-abc123");
    }

    [Fact]
    public async Task CC38_TooManyRequests_IsTreatedAsTransientAndRetried()
    {
        _recorder.Queue(HttpStatusCode.TooManyRequests);
        _recorder.Queue(HttpStatusCode.Accepted, headers: [("X-Message-Id", "sg-after-retry")]);

        var result = await CreateSut().SendAsync(Notification());

        result.Succeeded.Should().BeTrue();
        result.ProviderMessageId.Should().Be("sg-after-retry");
        _recorder.Calls.Should().HaveCount(2);
    }

    [Fact]
    public async Task CC37_BadRequest_IsNeverRetried()
    {
        _recorder.Queue(HttpStatusCode.BadRequest);

        var result = await CreateSut().SendAsync(Notification());

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ApplicationErrors.Notification.DELIVERY_FAILED);
        _recorder.Calls.Should().HaveCount(1);
    }
}

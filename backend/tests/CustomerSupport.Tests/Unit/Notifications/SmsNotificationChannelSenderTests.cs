using System.Net;
using System.Text;
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

public class SmsNotificationChannelSenderTests
{
    private const string ApiBaseUrl = "http://localhost:3001/mock/twilio/2010-04-01/Accounts/ACmockaccountsid/Messages.json";

    private readonly Mock<IExternalApiConfigurationProvider> _configProvider = new();
    private readonly RecordingHttpMessageHandler _recorder = new();

    public SmsNotificationChannelSenderTests()
    {
        _configProvider
            .Setup(p => p.GetConfig(NotificationGatewayConstants.SmsGatewayConfigName))
            .Returns(new ExternalApiConfig { BaseUrl = ApiBaseUrl, TimeoutSeconds = 30 });
    }

    private SmsNotificationChannelSender CreateSut() =>
        new(
            _configProvider.Object,
            new FakeHttpClientFactory(_recorder.Client),
            Options.Create(new ChannelOptions { SmsFrom = "CommandCenter" }),
            NullLogger<SmsNotificationChannelSender>.Instance);

    private static RenderedNotification Notification() =>
        new(null, null, "+15559998888", "Ticket reply",
            "Your ticket moved to Resolved.", "TICKET_REPLY", NotificationChannel.Sms, null);

    [Fact]
    public async Task CC39_BodyIsFormEncodedWithTwiliosFieldNames()
    {
        _recorder.Queue(HttpStatusCode.Created, body: """{"sid":"SM1234567890","status":"queued"}""");

        await CreateSut().SendAsync(Notification());

        var raw = Encoding.UTF8.GetString(_recorder.Calls[0].Body);
        var fields = System.Web.HttpUtility.ParseQueryString(raw);

        fields["To"].Should().Be("+15559998888");
        fields["From"].Should().Be("CommandCenter");
        fields["Body"].Should().Be("Your ticket moved to Resolved.");
    }

    [Fact]
    public async Task CC36_ProviderMessageIdIsTheTwilioSid()
    {
        _recorder.Queue(HttpStatusCode.Created, body: """{"sid":"SM1234567890","status":"queued"}""");

        var result = await CreateSut().SendAsync(Notification());

        result.Succeeded.Should().BeTrue();
        result.ProviderMessageId.Should().Be("SM1234567890");
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

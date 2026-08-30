using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.ValueObjects;
using CustomerSupport.Shared.Contracts.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Messaging;

public class SmsMessageConsumer(
    INotificationGateway gateway,
    ILogger<SmsMessageConsumer> logger) : IConsumer<SmsMessage>
{
    public async Task Consume(ConsumeContext<SmsMessage> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Dispatching SMS to {PhoneNumber}",
            message.PhoneNumber);

        var request = new NotificationDispatchRequest(
            TemplateCode: "SMS",
            RecipientUserId: null,
            Channels: new[] { NotificationChannel.Sms },
            Variables: new Dictionary<string, string>
            {
                ["Message"] = message.Message,
                ["PhoneNumber"] = message.PhoneNumber
            },
            Email: null,
            PhoneNumber: message.PhoneNumber,
            BypassUserSettings: true,
            DeduplicationKey: null,
            CorrelationId: context.MessageId?.ToString());

        var result = await gateway.SendAsync(request, context.CancellationToken);

        if (result.Succeeded)
        {
            logger.LogInformation("SMS dispatched successfully to {PhoneNumber}", message.PhoneNumber);
        }
        else
        {
            logger.LogWarning(
                "SMS dispatch failed for {PhoneNumber}: {Errors}",
                message.PhoneNumber,
                string.Join("; ", result.ChannelResults.Select(r => r.ErrorCode)));
        }
    }
}

using CustomerSupport.Application.Notifications;
using CustomerSupport.Domain.ValueObjects;
using CustomerSupport.Shared.Contracts.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Messaging;

public class EmailMessageConsumer(
    INotificationGateway gateway,
    ILogger<EmailMessageConsumer> logger) : IConsumer<EmailMessage>
{
    public async Task Consume(ConsumeContext<EmailMessage> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Dispatching email to {To} with subject {Subject}",
            message.To,
            message.Subject);

        var request = new NotificationDispatchRequest(
            TemplateCode: "Email",
            RecipientUserId: null,
            Channels: new[] { NotificationChannel.Email },
            Variables: new Dictionary<string, string>
            {
                ["Title"] = message.Subject,
                ["Message"] = message.Body,
                ["To"] = message.To
            },
            Email: message.To,
            PhoneNumber: null,
            BypassUserSettings: true,
            DeduplicationKey: null,
            CorrelationId: context.MessageId?.ToString());

        var result = await gateway.SendAsync(request, context.CancellationToken);

        if (result.Succeeded)
        {
            logger.LogInformation("Email dispatched successfully to {To}", message.To);
        }
        else
        {
            logger.LogWarning(
                "Email dispatch failed for {To}: {Errors}",
                message.To,
                string.Join("; ", result.ChannelResults.Select(r => r.ErrorCode)));
        }
    }
}

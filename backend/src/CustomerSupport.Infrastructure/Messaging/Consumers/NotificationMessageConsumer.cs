using CustomerSupport.Shared.Contracts.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Messaging;

public class NotificationMessageConsumer : IConsumer<NotificationMessage>
{
    private readonly ILogger<NotificationMessageConsumer> _logger;

    public NotificationMessageConsumer(ILogger<NotificationMessageConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<NotificationMessage> context)
    {
        var message = context.Message;
        
        _logger.LogInformation(
            "Processing notification for {IdentityNumber} with template {TemplateCode}",
            message.IdentityNumber,
            message.TemplateCode);

        await Task.Delay(100);
        
        _logger.LogInformation(
            "Notification processed successfully for {IdentityNumber}",
            message.IdentityNumber);
    }
}

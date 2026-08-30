using CustomerSupport.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.Messaging;

public class NoOpMessagePublisher(ILogger<NoOpMessagePublisher> logger) : IMessagePublisher
{
    public Task PublishAsync<T>(string topic, T message, CancellationToken ct = default) where T : class
    {
        logger.LogWarning("Message publishing is disabled. Skipping topic {Topic} for message type {MessageType}", topic, typeof(T).Name);
        return Task.CompletedTask;
    }
}

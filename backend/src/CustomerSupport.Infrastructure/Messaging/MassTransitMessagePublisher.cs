using CustomerSupport.Application.Interfaces;
using MassTransit;

namespace CustomerSupport.Infrastructure.Messaging;

public class MassTransitMessagePublisher : IMessagePublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitMessagePublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task PublishAsync<T>(string topic, T message, CancellationToken ct = default) where T : class
    {
        await _publishEndpoint.Publish(message, ct);
    }
}

namespace CustomerSupport.Application.Interfaces;

public interface IMessagePublisher
{
    Task PublishAsync<T>(string topic, T message, CancellationToken ct = default) where T : class;
}

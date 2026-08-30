using CustomerSupport.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Application.Events;

/// <summary>
/// Resolves every registered <see cref="IDomainEventHandler{TEvent}"/> for a raised event's type and
/// invokes it. A handler that throws is logged and swallowed — the aggregate data is already
/// committed by the time this runs, so a notification handler must never turn a successful write
/// into a request failure (spec AC-N4). When no handler is registered, forwarding to an empty set is
/// a no-op (AC-N4) and an empty event collection dispatches nothing (AC-N6).
/// </summary>
public sealed class DomainEventDispatcher(
    IServiceProvider serviceProvider,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger = logger;

    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var domainEvent in events)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());

            foreach (var handler in _serviceProvider.GetServices(handlerType).OfType<IDomainEventHandler>())
            {
                try
                {
                    await handler.HandleAsync(domainEvent, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Domain event handler {Handler} failed for {Event}",
                        handler.GetType().Name, domainEvent.GetType().Name);
                }
            }
        }
    }
}

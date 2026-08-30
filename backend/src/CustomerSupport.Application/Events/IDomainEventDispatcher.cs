using CustomerSupport.Domain.Events;

namespace CustomerSupport.Application.Events;

/// <summary>
/// Publishes collected domain events raised on aggregates to their <see cref="IDomainEventHandler{TEvent}"/>
/// consumers. Dispatched after a save commits, in a fresh scope, so a handler failure must never
/// fail an already-committed write.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct = default);
}

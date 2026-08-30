using CustomerSupport.Domain.Events;

namespace CustomerSupport.Application.Events;

/// <summary>
/// A consumer of a raised <see cref="IDomainEvent"/>. The non-generic <see cref="HandleAsync"/> is what
/// the <see cref="DomainEventDispatcher"/> invokes; the generic <c>Handle</c> is the typed
/// implementation a feature writes. The default interface method bridges the two so a handler only
/// implements <c>Handle(TEvent)</c>.
/// </summary>
public interface IDomainEventHandler
{
    Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}

public interface IDomainEventHandler<in TEvent> : IDomainEventHandler where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken ct = default);

    Task IDomainEventHandler.HandleAsync(IDomainEvent domainEvent, CancellationToken ct)
        => Handle((TEvent)domainEvent, ct);
}

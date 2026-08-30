namespace CustomerSupport.Domain.Events;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

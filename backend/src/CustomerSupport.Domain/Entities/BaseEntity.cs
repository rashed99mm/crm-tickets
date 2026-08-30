using CustomerSupport.Domain.Events;

namespace CustomerSupport.Domain.Entities;

public abstract class BaseEntity : IBaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public void MarkUpdated() => UpdatedAt = DateTime.UtcNow;
    public void SoftDelete() { IsDeleted = true; DeletedAt = DateTime.UtcNow; }
}

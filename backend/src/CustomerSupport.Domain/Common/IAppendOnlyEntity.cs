namespace CustomerSupport.Domain.Common;

/// <summary>
/// Marks an entity whose rows may only ever be inserted, never updated or soft/hard-deleted —
/// enforced by <c>AppDbContext</c>'s <c>SaveChanges</c> guard (ADR-0010). A row that must never
/// change once written (a history entry, a recorded message) implements this instead of the guard
/// being told about the concrete type by name.
/// </summary>
public interface IAppendOnlyEntity
{
}

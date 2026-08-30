namespace CustomerSupport.Domain.Interfaces;

/// <summary>
/// Issues the next human-readable ticket reference (<c>TKT-nnnnnn</c>).
///
/// A port rather than a method on the entity because the only race-free source of the number is a
/// database sequence, and the Domain must not know that SQL Server exists. <c>MAX(Reference) + 1</c>
/// was the alternative and it races under concurrent inserts, which the unique index would turn
/// into a 500.
/// </summary>
public interface ITicketReferenceGenerator
{
    Task<string> NextAsync(CancellationToken ct = default);
}

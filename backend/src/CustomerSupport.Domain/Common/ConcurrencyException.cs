namespace CustomerSupport.Domain.Common;

/// <summary>
/// Raised when a write loses an optimistic-concurrency race. The Infrastructure repository throws
/// this in place of <c>Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException</c> so the
/// Application layer can react to it without taking an Entity Framework dependency.
/// </summary>
public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }

    public ConcurrencyException(string message, Exception innerException)
        : base(message, innerException) { }
}

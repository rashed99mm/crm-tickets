namespace CustomerSupport.Application.Interfaces;

/// <summary>
/// Where the bytes live — outside the database, and outside a handler's knowledge (A18).
///
/// Declared here and implemented in Infrastructure so no handler ever learns whether storage is a
/// local disk, a network share or a bucket. Swapping the implementation is the only change that
/// moving to object storage should cost.
/// </summary>
public interface IFileStore
{
    /// <summary>
    /// Writes <paramref name="content"/> under a name the <em>caller's domain</em> generated —
    /// never a name a client supplied. Implementations must still assert the resolved location,
    /// because "the caller generates the name" is a promise and not a mechanism.
    /// </summary>
    Task SaveAsync(string storedFileName, Stream content, CancellationToken ct = default);

    /// <summary>The stored bytes, or <c>null</c> if nothing is stored under that name.</summary>
    Task<Stream?> OpenAsync(string storedFileName, CancellationToken ct = default);

    /// <summary>Removes the stored bytes. Absence is not an error — deletion is idempotent.</summary>
    Task DeleteAsync(string storedFileName, CancellationToken ct = default);
}

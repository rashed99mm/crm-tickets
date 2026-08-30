using CustomerSupport.Domain.Entities.Sla;

namespace CustomerSupport.Application.Interfaces;

/// <summary>
/// US-218 — selects the next escalation level for a ticket, as a method not an EF queryable, so the
/// breach scanner (Infrastructure) never reaches into the data layer itself. The concrete
/// implementation reads the seeded, active levels and returns the lowest one above the cursor —
/// terminal behavior falls out of there being no higher active level (spec addendum A11), not a
/// magic branch.
/// </summary>
public interface IEscalationLevelProvider
{
    /// <summary>
    /// Returns the next active <see cref="EscalationLevel"/> above the ticket's current
    /// <paramref name="currentLevel"/>, or null when the current level is terminal (no higher
    /// active level exists). A null or unrecognised cursor selects the lowest active level.
    /// </summary>
    Task<EscalationLevel?> NextLevelAsync(string currentLevel, CancellationToken ct = default);
}

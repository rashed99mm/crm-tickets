using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Sla;

/// <summary>
/// US-218 — reads the seeded, active escalation levels and returns the lowest one above the ticket's
/// current level. Ordering follows <see cref="EscalationLevel.BreachMinutes"/> ascending: a higher
/// breach threshold denotes a higher level, which is how the seeded defaults are written. Terminal
/// behavior is the absence of a higher active level (spec addendum A11), never a magic branch.
/// </summary>
public sealed class EscalationLevelProvider(AppDbContext db) : IEscalationLevelProvider
{
    public async Task<EscalationLevel?> NextLevelAsync(string currentLevel, CancellationToken ct = default)
    {
        var levels = await db.Set<EscalationLevel>().IgnoreQueryFilters()
            .OrderBy(l => l.BreachMinutes)
            .ToListAsync(ct);

        return EscalationLevel.NextFrom(levels, currentLevel);
    }
}

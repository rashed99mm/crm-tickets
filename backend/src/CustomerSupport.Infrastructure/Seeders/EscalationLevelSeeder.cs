using CustomerSupport.Domain.Entities.Sla;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Seeders;

/// <summary>
/// US-218 — the fixed escalation ladder (Level1/Level2), seeded by a developer and read-only this
/// pass (spec addendum A9: no admin endpoint). Idempotent for the same reason <see cref="CategorySeeder"/>
/// is: it runs on every internal-host start, so a race between two starting hosts is ordinary, not
/// exceptional.
/// </summary>
public class EscalationLevelSeeder(AppDbContext db)
{
    public static readonly Guid Level1Id = new("00000000-0000-0000-0000-0000000000E1");
    public static readonly Guid Level2Id = new("00000000-0000-0000-0000-0000000000E2");

    /// <summary>The default ladder — the first level is the lowest breach threshold; levels ascend
    /// by <see cref="EscalationLevel.BreachMinutes"/>. No "Level3": a ticket beyond Level2 has no
    /// higher active level, which is what makes Level2 terminal (spec addendum A11).</summary>
    public static readonly (string Level, int BreachMinutes, string? TargetRole, Guid Id)[] Defaults =
    [
        ("Level1", 60, "Agent", Level1Id),
        ("Level2", 240, "Supervisor", Level2Id),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var missing = await db.EscalationLevels.IgnoreQueryFilters()
            .Select(l => l.Level)
            .ToListAsync(ct);

        var existing = new HashSet<string>(missing);
        var hasMissing = Defaults.Any(d => !existing.Contains(d.Level));

        if (!hasMissing)
        {
            return;
        }

        foreach (var (level, breachMinutes, targetRole, id) in Defaults)
        {
            if (!existing.Contains(level))
            {
                db.EscalationLevels.Add(EscalationLevel.Create(level, breachMinutes, targetRole, id));
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Losing the insert race to another host starting at the same moment is the expected
            // outcome, not a failure — but only if the rows are actually there afterwards.
            foreach (var entry in db.ChangeTracker.Entries<EscalationLevel>().ToList())
            {
                entry.State = EntityState.Detached;
            }

            var nowExisting = (await db.EscalationLevels.IgnoreQueryFilters()
                    .Select(l => l.Level).ToListAsync(ct))
                .ToHashSet();

            if (Defaults.Any(d => !nowExisting.Contains(d.Level)))
            {
                throw;
            }
        }
    }
}

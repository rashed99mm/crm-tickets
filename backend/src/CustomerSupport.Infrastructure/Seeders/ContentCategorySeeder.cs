using CustomerSupport.Domain.Entities.Content;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Seeders;

/// <summary>
/// Starter taxonomy for the customer-facing knowledge base (FEAT-11). The category tree the
/// portal's home page surfaces is small enough to seed by hand: six root categories, each with
/// two or three sub-topics. Authors extend it through the admin endpoint, so this seeder is
/// idempotent — it inserts only what is absent, and a rolling deploy (which restarts the host
/// more than once during a release) does not duplicate rows.
/// </summary>
public class ContentCategorySeeder(AppDbContext db)
{
    /// <summary>The starter categories, paired with their slug, sub-categories, and a sort order
    /// the portal uses to keep the bento grid in a stable order across releases.</summary>
    public static readonly IReadOnlyList<SeedCategory> Seeds =
    [
        new("Getting Started", 1, [
            new("Quick start", 1),
            new("Account setup", 2),
        ]),
        new("Account & Billing", 2, [
            new("Subscriptions", 1),
            new("Invoices", 2),
            new("Payment methods", 3),
        ]),
        new("Troubleshooting", 3, [
            new("Common errors", 1),
            new("Connectivity", 2),
        ]),
        new("Integrations", 4, [
            new("REST API", 1),
            new("Webhooks", 2),
        ]),
        new("Security & Privacy", 5, [
            new("Two-factor auth", 1),
            new("Data handling", 2),
        ]),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existing = await db.ContentCategories
            .IgnoreQueryFilters()
            .Select(c => c.Name)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existing, StringComparer.Ordinal);

        foreach (var seed in Seeds)
        {
            if (existingSet.Contains(seed.Name))
            {
                continue;
            }

            db.ContentCategories.Add(ContentCategory.Create(seed.Name, null, seed.SortOrder));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Same race-tolerance pattern as CategorySeeder: another host may have inserted the
            // same row between our read and our write. Detach the failed inserts, re-read, and
            // only swallow if the rows really did land.
            foreach (var entry in db.ChangeTracker.Entries<ContentCategory>().ToList())
            {
                entry.State = EntityState.Detached;
            }

            var after = await db.ContentCategories
                .IgnoreQueryFilters()
                .Select(c => c.Name)
                .ToListAsync(ct);
            var stillMissing = Seeds.Where(s => !after.Contains(s.Name)).Select(s => s.Name).ToList();
            if (stillMissing.Count > 0)
            {
                throw;
            }
        }

        // Seed sub-categories: need the parent ids, so this is a second pass after the roots
        // have landed (whether by us or by another host).
        var parents = await db.ContentCategories
            .Where(c => c.ParentId == null)
            .ToDictionaryAsync(c => c.Name, c => c.Id, ct);

        var children = await db.ContentCategories
            .Where(c => c.ParentId != null)
            .Select(c => c.Name)
            .ToListAsync(ct);
        var childSet = new HashSet<string>(children, StringComparer.Ordinal);

        foreach (var seed in Seeds)
        {
            if (!parents.TryGetValue(seed.Name, out var parentId))
            {
                continue;
            }

            foreach (var sub in seed.SubCategories)
            {
                if (childSet.Contains(sub.Name))
                {
                    continue;
                }

                db.ContentCategories.Add(ContentCategory.Create(sub.Name, parentId, sub.SortOrder));
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            foreach (var entry in db.ChangeTracker.Entries<ContentCategory>().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    public sealed record SeedCategory(string Name, int SortOrder, IReadOnlyList<SeedSubCategory> SubCategories);
    public sealed record SeedSubCategory(string Name, int SortOrder);
}

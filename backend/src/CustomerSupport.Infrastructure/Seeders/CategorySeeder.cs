using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Seeders;

/// <summary>
/// The fixed ticket category list (assumption A4): seeded by a developer, read-only in S1.
///
/// Fixed rather than user-editable because reporting needs something stable to group by, and
/// free-text categories are refused for the same reason (BR-14).
/// </summary>
public class CategorySeeder(AppDbContext db)
{
    /// <summary>
    /// The seeded list. Deliberately short — four buckets an agent can choose between without
    /// thinking beats twenty that invite miscategorisation.
    /// </summary>
    public static readonly string[] Names = ["Technical", "Billing", "Account", "General"];

    /// <summary>
    /// Idempotent: this runs on every start of the internal host, so it must add only what is
    /// absent. Matching on name is what makes that true, and <c>UX_Categories_Name</c> is the
    /// backstop when two hosts start at once — which a rolling deploy does every time.
    /// </summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var missing = await MissingNamesAsync(ct);
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var name in missing)
        {
            db.Categories.Add(Category.Create(name));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Losing the insert race is not a startup failure — it means someone else created the
            // categories, which is the outcome this method wanted. But that is a claim worth
            // checking rather than assuming, because the same exception covers genuine faults.
            // Detach the failed inserts so the context stays usable, then re-read: if the rows are
            // really there, carry on; if not, the exception was about something else and must not
            // be swallowed.
            foreach (var entry in db.ChangeTracker.Entries<Category>().ToList())
            {
                entry.State = EntityState.Detached;
            }

            if ((await MissingNamesAsync(ct)).Count > 0)
            {
                throw;
            }
        }
    }

    private async Task<List<string>> MissingNamesAsync(CancellationToken ct)
    {
        var existing = await db.Categories
            .IgnoreQueryFilters()
            .Select(c => c.Name)
            .ToListAsync(ct);

        return [.. Names.Except(existing)];
    }
}

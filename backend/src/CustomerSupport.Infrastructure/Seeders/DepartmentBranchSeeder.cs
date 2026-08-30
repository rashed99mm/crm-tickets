using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Seeders;

/// <summary>
/// The default department and branch (US-305, AC-118) — a home for data that predates
/// organisational grouping. Well-known ids, not name-matching, because a downstream feature needs
/// to reference "the default department" directly.
/// </summary>
public class DepartmentBranchSeeder(AppDbContext db)
{
    public static readonly Guid DefaultDepartmentId = new("00000000-0000-0000-0000-000000000001");
    public static readonly Guid DefaultBranchId = new("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Idempotent for the same reason <see cref="CategorySeeder"/> is: this runs on every host
    /// start, so a race between two starting hosts is ordinary, not exceptional.
    /// </summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var hasDepartment = await db.Departments.IgnoreQueryFilters()
            .AnyAsync(d => d.Id == DefaultDepartmentId, ct);
        var hasBranch = await db.Branches.IgnoreQueryFilters()
            .AnyAsync(b => b.Id == DefaultBranchId, ct);

        if (!hasDepartment)
        {
            db.Departments.Add(Department.Create("General", managerId: null, id: DefaultDepartmentId));
        }

        if (!hasBranch)
        {
            db.Branches.Add(Branch.Create("Head Office", region: null, timezone: "UTC", id: DefaultBranchId));
        }

        if (!hasDepartment || !hasBranch)
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Same reasoning as CategorySeeder: losing the insert race to another host starting
                // at the same moment is the expected outcome, not a failure — but only if the rows
                // are actually there afterwards.
                foreach (var entry in db.ChangeTracker.Entries<Department>().ToList())
                {
                    entry.State = EntityState.Detached;
                }

                foreach (var entry in db.ChangeTracker.Entries<Branch>().ToList())
                {
                    entry.State = EntityState.Detached;
                }

                var stillMissing = !await db.Departments.IgnoreQueryFilters().AnyAsync(d => d.Id == DefaultDepartmentId, ct)
                    || !await db.Branches.IgnoreQueryFilters().AnyAsync(b => b.Id == DefaultBranchId, ct);

                if (stillMissing)
                {
                    throw;
                }
            }
        }
    }
}

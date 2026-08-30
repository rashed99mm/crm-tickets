using CustomerSupport.Domain.Entities.Organisation;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Infrastructure.Seeders;

/// <summary>
/// The default team per department (US-905, AC-508). Well-known id, not name-matching, because a
/// downstream feature needs to reference "the default team" directly.
/// </summary>
public class TeamSeeder(AppDbContext db)
{
    public static readonly Guid DefaultTeamId = new("00000000-0000-0000-0000-000000000002");

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var hasTeam = await db.Teams.IgnoreQueryFilters()
            .AnyAsync(t => t.Id == DefaultTeamId, ct);

        if (!hasTeam)
        {
            db.Teams.Add(Team.Create(
                "General Department Team",
                DepartmentBranchSeeder.DefaultDepartmentId,
                managerId: null,
                id: DefaultTeamId));
        }

        if (!hasTeam)
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                foreach (var entry in db.ChangeTracker.Entries<Team>().ToList())
                {
                    entry.State = EntityState.Detached;
                }

                var stillMissing = !await db.Teams.IgnoreQueryFilters()
                    .AnyAsync(t => t.Id == DefaultTeamId, ct);

                if (stillMissing)
                {
                    throw;
                }
            }
        }
    }
}

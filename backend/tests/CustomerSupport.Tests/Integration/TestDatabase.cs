using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// Brings the shared test database up to the current migration, once per test process.
///
/// This has to happen **before any host starts**, not from inside a factory's service provider:
/// the internal host seeds ticket categories during start-up, and resolving anything from
/// <c>WebApplicationFactory.Services</c> is what starts it. A factory that migrated through its own
/// provider would therefore be asked to seed into a table it had not created yet.
///
/// xUnit runs test classes in parallel, so several factories reach this at once — hence the lock
/// and the one-shot flag rather than a plain call per factory.
/// </summary>
internal static class TestDatabase
{
    // Each test process gets a fresh database. This keeps repeated local runs and parallel test
    // classes from inheriting provider-message ids, users, or content from an earlier run.
    public static string ConnectionString { get; } =
        $"Server=(localdb)\\MSSQLLocalDB;Database=CustomerSupportCrmTest_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _migrated;

    public static async Task EnsureMigratedAsync()
    {
        if (_migrated)
        {
            return;
        }

        await Gate.WaitAsync();
        try
        {
            if (_migrated)
            {
                return;
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;

            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync();

            _migrated = true;
        }
        finally
        {
            Gate.Release();
        }
    }
}

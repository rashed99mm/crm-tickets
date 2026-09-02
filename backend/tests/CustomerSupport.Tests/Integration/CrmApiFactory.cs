using System.Net.Http.Headers;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// The shared host for the CRM endpoint tests, over a **real LocalDB database**.
///
/// Not the in-memory provider, and this is not a preference. <c>AC-9</c> is a filtered unique index
/// and <c>AC-41</c> is a <c>rowversion</c>; the in-memory provider honours neither, so a suite built
/// on it would report both criteria as passing while the real database rejected the same requests.
/// That failure mode — green tests over a broken feature — is worse than no test at all.
/// </summary>
public class CrmApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Where this host's uploaded bytes land — a directory of its own, per factory instance.
    ///
    /// The attachment criteria (AC-23, AC-24, AC-25) are assertions about the <em>filesystem</em>,
    /// not about status codes, so a test has to be able to count what is on disk. A shared or
    /// default root would have it counting some other test's uploads.
    /// </summary>
    public string StorageRoot { get; } =
        Path.Combine(Path.GetTempPath(), "customersupport-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", TestDatabase.ConnectionString);
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-at-least-32-characters-long");
        // ConfigureMessaging reads this raw config key directly, not IWebHostEnvironment,
        // so UseEnvironment alone does not satisfy it.
        builder.UseSetting("Messaging:Required", "false");
        builder.UseSetting("FileStorage:RootPath", StorageRoot);
        builder.UseEnvironment("Development");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        try
        {
            if (Directory.Exists(StorageRoot))
            {
                Directory.Delete(StorageRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp directory is untidy, not a test failure. The host may still be
            // releasing a stream handle as the factory tears down.
        }
    }

    /// <summary>Brings the shared test database up to the current migration. See <see cref="TestDatabase"/>.</summary>
    public Task EnsureDatabaseAsync() => TestDatabase.EnsureMigratedAsync();

    /// <summary>Seeds the WhatsAppGateway configuration row the reply path's sender reads.</summary>
    public Task SeedWhatsAppGatewayAsync(string baseUrl) => GatewayTestData.SeedWhatsAppGatewayAsync(Services, baseUrl);

    /// <summary>Seeds the EmailGateway configuration the outbound email sender dispatches through (CC-44).</summary>
    public Task SeedEmailGatewayAsync(string baseUrl) => GatewayTestData.SeedEmailGatewayAsync(Services, baseUrl);

    /// <summary>
    /// A fresh user per test, created straight through Identity. Each test owning its own user is
    /// what lets the suite run against one shared database without ordering dependencies.
    /// </summary>
    public async Task<(ApplicationUser User, string Password)> CreateUserAsync(params string[] roles)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        var email = $"crm-{Guid.NewGuid():N}@test.local";
        const string password = "Test-Password-456";

        var user = ApplicationUser.Create(email, email, "Test", "User");
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue(
            string.Join(", ", result.Errors.Select(e => e.Description)));

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                // Check-then-create races: test classes run in parallel, each starts a host, and
                // IdentitySeeder now seeds Agent and Supervisor on every start. Losing the race
                // means the role exists, which is all this needed — RoleNameIndex settles it.
                try
                {
                    await roleManager.CreateAsync(ApplicationRole.Create(role, role));
                }
                catch (DbUpdateException)
                {
                }
            }

            await userManager.AddToRoleAsync(user, role);
        }

        return (user, password);
    }

    /// <summary>An authenticated client for a brand-new user.</summary>
    public async Task<(HttpClient Client, ApplicationUser User)> CreateAuthenticatedClientAsync(params string[] roles)
    {
        var (user, password) = await CreateUserAsync(roles);
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/Auth/login", new { email = user.Email, password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<Response<LoginData>>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);

        return (client, user);
    }

    /// <summary>Seeds a category directly, so ticket tests do not depend on the seeder having run.</summary>
    public async Task<Guid> EnsureCategoryAsync(string name)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.Categories.FirstOrDefaultAsync(c => c.Name == name);
        if (existing is not null)
        {
            return existing.Id;
        }

        var category = Domain.Entities.Tickets.Category.Create(name);
        db.Categories.Add(category);

        try
        {
            await db.SaveChangesAsync();
            return category.Id;
        }
        catch (DbUpdateException)
        {
            // The host's own CategorySeeder may have inserted the same name between the read and
            // the write — test classes run in parallel and each starts a host. Whoever won, the
            // category now exists, which is all this helper promised.
            db.ChangeTracker.Clear();
            var winner = await db.Categories.FirstAsync(c => c.Name == name);
            return winner.Id;
        }
    }

    public sealed record LoginData(string AccessToken, string RefreshToken);
}

/// <summary>The paged envelope shape, matching <c>PaginatedList&lt;T&gt;</c> on the wire.</summary>
public sealed record PagedData<T>(
    List<T> Items,
    int PageIndex,
    int PageSize,
    int TotalCount);

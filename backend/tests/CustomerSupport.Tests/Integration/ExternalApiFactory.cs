extern alias externalapi;

using CustomerSupport.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> over the customer-facing host, so the portal
/// journey can be exercised end-to-end against the real database (US-401..US-409). The external
/// host is the only one that sets <c>IsPortalRegistration</c>, so a customer created through this
/// factory is the one that gets a linked <c>Customer</c> row (PJ-2) — the internal host would not.
/// <c>PortalController</c> stands in for the entry point merely to anchor the factory to this
/// assembly's <c>Program</c> (the generated <c>Program</c> type is internal).
/// </summary>
public sealed class ExternalApiFactory : WebApplicationFactory<externalapi::CustomerSupport.ExternalApi.Controllers.PortalController>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", TestDatabase.ConnectionString);
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-at-least-32-characters-long");
        builder.UseSetting("Messaging:Required", "false");
        builder.UseEnvironment("Development");
    }

    public Task EnsureDatabaseAsync() => TestDatabase.EnsureMigratedAsync();

    /// <summary>Seeds a category directly so the portal submit picker has something to choose.</summary>
    public async Task<Guid> EnsureCategoryAsync(string name)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.Categories.FirstOrDefaultAsync(c => c.Name == name);
        if (existing is not null)
        {
            return existing.Id;
        }

        var category = CustomerSupport.Domain.Entities.Tickets.Category.Create(name);
        db.Categories.Add(category);

        try
        {
            await db.SaveChangesAsync();
            return category.Id;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return (await db.Categories.FirstAsync(c => c.Name == name)).Id;
        }
    }
}
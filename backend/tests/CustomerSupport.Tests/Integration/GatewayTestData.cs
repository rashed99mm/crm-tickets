using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Entities.ExternalApis;
using CustomerSupport.Domain.Entities.Tickets;
using CustomerSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// Provisions the WhatsApp gateway the sender and the signature verifier read, straight into the
/// shared test database through whichever host resolves the service provider. Both factories share
/// this database, so a webhook reaching the external host and a reply leaving the internal host
/// both see the same row. Base URLs are always test-local sandboxes (spec A11), never live Meta.
/// </summary>
internal static class GatewayTestData
{
    public const string WhatsAppAppSecret = "whatsapp-app-secret-for-tests-only";

    public static async Task SeedWhatsAppGatewayAsync(IServiceProvider services, string baseUrl)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        if (!await db.Categories.AnyAsync(c => c.Name == "General" && c.IsActive))
        {
            db.Categories.Add(Category.Create("General"));
        }

        var existing = await db.Set<ExternalApiConfiguration>()
            .SingleOrDefaultAsync(c => c.Name == "WhatsAppGateway");

        if (existing is null)
        {
            db.Set<ExternalApiConfiguration>().Add(ExternalApiConfiguration.Create(
                "WhatsAppGateway",
                baseUrl: baseUrl,
                timeoutSeconds: 30,
                authType: "Bearer",
                authValue: protector.Protect(WhatsAppAppSecret)));
        }
        else
        {
            existing.UpdateConfig(baseUrl, 30);
            existing.UpdateAuth(
                authType: "Bearer",
                authValue: protector.Protect(WhatsAppAppSecret));
            db.Set<ExternalApiConfiguration>().Update(existing);
        }

        await db.SaveChangesAsync();

        // The provider caches its rows; the next read must see the new config.
        await scope.ServiceProvider.GetRequiredService<IExternalApiConfigurationProvider>().ReloadAsync();
    }
}

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

    /// <summary>
    /// `baseUrl` is stored as given — this helper does not compose a path. `WhatsAppWebhookTests`
    /// passes a complete, self-contained fake URL it never dereferences (CC-8/CC-9 only exercise
    /// inbound ingestion); `WhatsAppOutboundReplyTests` passes a real, reachable URL because its
    /// sender actually POSTs to it, so that call site is responsible for pointing at the stub's
    /// mapped route itself.
    /// </summary>
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

        // WhatsAppAppSecret plays two genuinely different roles that happen to share one test
        // constant: MetaSignatureVerifier reads Auth.Value to verify an inbound webhook's HMAC
        // signature (CC-8/CC-9/CC-5), while the outbound sender's Bearer auth reads Auth.Token
        // (CC-51) — two different ExternalApiAuthConfig fields, not one. Seeding only Token (as an
        // earlier version of this fix did) leaves Value empty and every inbound signature check
        // fails; seeding only Value (as the original code did) leaves the outbound Authorization
        // header empty. Both are set to the same protected secret.
        var protectedSecret = protector.Protect(WhatsAppAppSecret);

        if (existing is null)
        {
            db.Set<ExternalApiConfiguration>().Add(ExternalApiConfiguration.Create(
                "WhatsAppGateway",
                baseUrl: baseUrl,
                timeoutSeconds: 30,
                authType: "Bearer",
                authValue: protectedSecret,
                authToken: protectedSecret));
        }
        else
        {
            existing.UpdateConfig(baseUrl, 30);
            existing.UpdateAuth(
                authType: "Bearer",
                authValue: protectedSecret,
                authToken: protectedSecret);
            db.Set<ExternalApiConfiguration>().Update(existing);
        }

        await db.SaveChangesAsync();

        // The provider caches its rows; the next read must see the new config.
        await scope.ServiceProvider.GetRequiredService<IExternalApiConfigurationProvider>().ReloadAsync();
    }

    public const string SmsAuthToken = "twilio-auth-token-for-tests-only";

    /// <summary>
    /// Provisions the SmsGateway row TwilioSignatureVerifier reads its account auth token from
    /// (CC-40/CC-41). Unlike WhatsApp, only Auth.Value is strictly needed here: the inbound verifier
    /// reads Value. Token is set to the same secret anyway so an outbound SMS test can reuse the row.
    /// </summary>
    public static async Task SeedSmsGatewayAsync(IServiceProvider services, string baseUrl)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        if (!await db.Categories.AnyAsync(c => c.Name == "General" && c.IsActive))
        {
            db.Categories.Add(Category.Create("General"));
        }

        var protectedSecret = protector.Protect(SmsAuthToken);
        var existing = await db.Set<ExternalApiConfiguration>()
            .SingleOrDefaultAsync(c => c.Name == "SmsGateway");

        if (existing is null)
        {
            db.Set<ExternalApiConfiguration>().Add(ExternalApiConfiguration.Create(
                "SmsGateway",
                baseUrl: baseUrl,
                timeoutSeconds: 30,
                authType: "Bearer",
                authValue: protectedSecret,
                authToken: protectedSecret));
        }
        else
        {
            existing.UpdateConfig(baseUrl, 30);
            existing.UpdateAuth(authType: "Bearer", authValue: protectedSecret, authToken: protectedSecret);
            db.Set<ExternalApiConfiguration>().Update(existing);
        }

        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IExternalApiConfigurationProvider>().ReloadAsync();
    }

    public const string EmailApiKey = "sendgrid-api-key-for-tests-only";

    /// <summary>
    /// Provisions the EmailGateway row EmailNotificationChannelSender dispatches through (CC-44).
    /// authToken carries the credential because the sender's Bearer branch reads Auth.Token — the
    /// same Value/Token distinction CC-51 turned on.
    /// </summary>
    public static async Task SeedEmailGatewayAsync(IServiceProvider services, string baseUrl)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        if (!await db.Categories.AnyAsync(c => c.Name == "General" && c.IsActive))
        {
            db.Categories.Add(Category.Create("General"));
        }

        var protectedSecret = protector.Protect(EmailApiKey);
        var existing = await db.Set<ExternalApiConfiguration>()
            .SingleOrDefaultAsync(c => c.Name == "EmailGateway");

        if (existing is null)
        {
            db.Set<ExternalApiConfiguration>().Add(ExternalApiConfiguration.Create(
                "EmailGateway",
                baseUrl: baseUrl,
                timeoutSeconds: 30,
                authType: "Bearer",
                authValue: protectedSecret,
                authToken: protectedSecret));
        }
        else
        {
            existing.UpdateConfig(baseUrl, 30);
            existing.UpdateAuth(authType: "Bearer", authValue: protectedSecret, authToken: protectedSecret);
            db.Set<ExternalApiConfiguration>().Update(existing);
        }

        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IExternalApiConfigurationProvider>().ReloadAsync();
    }
}

using CustomerSupport.Api.Shared.Middleware;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Infrastructure.Seeders;
using Hangfire;
using Serilog;

namespace CustomerSupport.Api.Shared.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UsePlatformPipeline(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UsePlatformApiDocumentation();
        app.UseSerilogRequestLogging();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseHsts();
        app.UseHttpsRedirection();
        app.UseCors();
        app.UseRateLimiter();
        app.UseRouting();
        // BEFORE authentication and authorization, so it WRAPS them. Registered after them it
        // would never run at all: authorization short-circuits the pipeline, and everything
        // downstream of the short circuit is skipped. It has to be upstream to see the result.
        app.UseMiddleware<AuthorizationEnvelopeMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseOutputCache();

        return app;
    }

    public static async Task UsePlatformDataSeedingAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var identitySeeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await identitySeeder.SeedAsync();
        Log.Information("Identity data seeding completed");

        var permissionSeeder = scope.ServiceProvider.GetRequiredService<PermissionSeeder>();
        await permissionSeeder.SeedAsync();
        Log.Information("Permission data seeding completed");

        // The fixed ticket category list (A4). Internal host only: seeding is administrative, and
        // the external host deliberately does not do it (BASE-7).
        var categorySeeder = scope.ServiceProvider.GetRequiredService<CategorySeeder>();
        await categorySeeder.SeedAsync();
        Log.Information("Ticket category seeding completed");

        // The default department and branch (FEAT-16, AC-118). Internal host only, same reasoning
        // as the category seed above.
        var departmentBranchSeeder = scope.ServiceProvider.GetRequiredService<DepartmentBranchSeeder>();
        await departmentBranchSeeder.SeedAsync();
        Log.Information("Department/branch seeding completed");

        var teamSeeder = scope.ServiceProvider.GetRequiredService<TeamSeeder>();
        await teamSeeder.SeedAsync();
        Log.Information("Team seeding completed");

        // The fixed escalation ladder (US-218, spec addendum A9). Internal host only, same reasoning
        // as the category seed above.
        var escalationLevelSeeder = scope.ServiceProvider.GetRequiredService<EscalationLevelSeeder>();
        await escalationLevelSeeder.SeedAsync();
        Log.Information("Escalation level seeding completed");

        var quickReplySeeder = scope.ServiceProvider.GetRequiredService<QuickReplySeeder>();
        await quickReplySeeder.SeedAsync();
        Log.Information("Quick reply seeding completed");

        // FEAT-11 — the public knowledge base's starter category taxonomy (US-503). Internal host
        // only, same reasoning as the other administrative seeders above.
        var contentCategorySeeder = scope.ServiceProvider.GetRequiredService<ContentCategorySeeder>();
        await contentCategorySeeder.SeedAsync();
        Log.Information("Knowledge base category seeding completed");

        var contentSeeder = scope.ServiceProvider.GetRequiredService<ContentSeeder>();
        await contentSeeder.SeedAsync();
        Log.Information("Knowledge base content seeding completed");

        var provider = scope.ServiceProvider.GetRequiredService<IExternalApiConfigurationProvider>();
        await provider.ReloadAsync();
        Log.Information("External API configuration provider cache loaded");
    }

    public static WebApplication MapPlatformEndpoints(this WebApplication app)
    {
        app.MapControllers();
        app.MapHub<CustomerSupport.Api.Shared.Hubs.MainHub>("/hubs/main").RequireAuthorization("Authenticated");

        if (!app.Environment.IsDevelopment())
        {
            app.MapHangfireDashboard("/jobs").RequireAuthorization("Admin");
        }

        app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
        app.MapHealthChecks("/health/ready");

        return app;
    }
}

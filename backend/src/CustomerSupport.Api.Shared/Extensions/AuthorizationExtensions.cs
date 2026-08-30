using CustomerSupport.Api.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace CustomerSupport.Api.Shared.Extensions;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddPlatformAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy("Admin", policy => policy.RequireRole("SuperAdmin", "Admin"))
            .AddPolicy("User", policy => policy.RequireRole("SuperAdmin", "Admin", "User"))
            .AddPolicy("ContentManager", policy => policy.RequireRole("SuperAdmin", "Admin", "ContentManager"))
            .AddPolicy("StateRepresentative", policy => policy.RequireRole("SuperAdmin", "Admin", "StateRepresentative"))
            .AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser())
            // The support domain (ADR-0012). Supervisor is granted wherever Admin is, so an
            // administrator is not locked out of supervisory actions - but Admin is deliberately
            // NOT an Agent: "can administer the platform" and "works a support queue" are
            // different claims, and AC-44 turns on the second.
            .AddPolicy("Supervisor", policy => policy.RequireRole("SuperAdmin", "Supervisor", "Admin"))
            .AddPolicy("Agent", policy => policy.RequireRole("SuperAdmin", "Agent"))
            .AddPolicy("ChatSupport", policy => policy.RequireRole("SuperAdmin", "Agent", "Supervisor", "Admin"));

        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy("UserManagement", policy =>
            {
                policy.RequireRole("SuperAdmin", "Admin");
                policy.AddRequirements(new PermissionRequirement("user.manage"));
            });

        return services;
    }
}

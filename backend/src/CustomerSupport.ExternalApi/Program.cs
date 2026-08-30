using CustomerSupport.Api.Shared.Extensions;
using CustomerSupport.Api.Shared.Hubs;
using CustomerSupport.ExternalApi.Middleware;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);
var configuredUrls = builder.Configuration["Urls"];

if (!string.IsNullOrWhiteSpace(configuredUrls))
{
    builder.WebHost.UseUrls(configuredUrls);
}

builder.Host.AddPlatformLogging(builder.Configuration);

// The same composition core as the internal host: identical envelope,
// identical pipeline order, identical serialization. A customer-facing
// deployment that answered in a different shape would be a second contract
// to maintain and a second place for a defect to hide.
builder.Services
    .AddPlatformOpenApi()
    .AddPlatformApiVersioning()
    .AddPlatformPersistence(builder.Configuration)
    .AddPlatformInfrastructureServices(builder.Configuration, "CustomerSupport.ExternalApi")
    .AddPlatformAuthentication(builder.Configuration)
    .AddPlatformAuthorization()
    .AddPlatformWebApi(builder.Configuration, builder.Environment);

// API-key auth for machine-to-machine access to the public surface (AC-144).
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);
builder.Services.AddAuthorization();

var app = builder.Build();

app.UsePlatformPipeline();
app.MapPlatformEndpoints();

// CC-14 / CC-16 — the anonymous live-chat hub. It is deliberately NOT part of the shared
// MapPlatformEndpoints (which maps the authenticated /hubs/main on both hosts): only the
// customer-facing host should accept anonymous chat connections. The hub itself validates the
// opaque session token in the query string; no JWT is required, so it maps without the
// "Authenticated" policy.
app.MapHub<ChatHub>("/hubs/chat");

// Deliberately NOT seeding here. Seeding is an administrative act and belongs
// to the internal host; a customer-facing deployment must not create accounts
// or reference data on start-up.

app.Run();

/// <summary>Exposed so WebApplicationFactory can host the external API in tests.</summary>
public partial class Program;

using CustomerSupport.Api.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);
var configuredUrls = builder.Configuration["Urls"];

if (!string.IsNullOrWhiteSpace(configuredUrls))
{
    builder.WebHost.UseUrls(configuredUrls);
}

builder.Host.AddPlatformLogging(builder.Configuration);

builder.Services
    .AddPlatformOpenApi()
    .AddPlatformApiVersioning()
    .AddPlatformPersistence(builder.Configuration)
    .AddPlatformInfrastructureServices(builder.Configuration, "CustomerSupport.InternalApi")
    .AddPlatformAuthentication(builder.Configuration)
    .AddPlatformAuthorization()
    .AddPlatformWebApi(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UsePlatformPipeline();
app.MapPlatformEndpoints();
if (builder.Configuration.GetValue("SeedData", true))
{
    await app.UsePlatformDataSeedingAsync();
}

app.Run();

public partial class Program;

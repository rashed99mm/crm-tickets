using CustomerSupport.Application;
using CustomerSupport.Infrastructure;
using CustomerSupport.Infrastructure.Configuration;
using CustomerSupport.Infrastructure.ExternalApis;
using CustomerSupport.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace CustomerSupport.Api.Shared.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddPlatformPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is required");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                sql.CommandTimeout(60);
            });
        });

        return services;
    }

    public static IServiceCollection AddPlatformInfrastructureServices(this IServiceCollection services, IConfiguration configuration, string appName)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is required");
        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";

        services.RegisterPlatformApplication();
        services.RegisterPlatformInfrastructure(configuration);
        services.AddDataProtection();
        services.Configure<DateTimeSettings>(configuration.GetSection("DateTimeSettings"));
        services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);

        // Register direct Redis multiplexer for advanced operations (prefix removal, etc.)
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = ConfigurationOptions.Parse(redisConnection);
            config.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(config);
        });

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(configuration.GetConnectionString("HangfireDb") ?? connectionString));
        services.AddHangfireServer();

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(appName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        services.AddExternalApiServices();

        return services;
    }
}

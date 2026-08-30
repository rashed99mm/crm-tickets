using CustomerSupport.Application.Localization;
using CustomerSupport.Application.Messages;
using CustomerSupport.Infrastructure.Localization;
using CustomerSupport.Infrastructure.Messages;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;
using System.Text.Json.Serialization;
using CustomerSupport.Api.Shared.Serialization;
using System.Threading.RateLimiting;

namespace CustomerSupport.Api.Shared.Extensions;

public static class WebApiServiceExtensions
{
    public static IServiceCollection AddPlatformWebApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                // AC-54. EF hands back DateTimeKind.Unspecified, so without these the wire carries
                // no Z and every client reads UTC as local time.
                options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
                options.JsonSerializerOptions.Converters.Add(new UtcNullableDateTimeConverter());
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });
        services.AddSignalR();
        services.AddPlatformRateLimiting();
        services.AddPlatformCors(configuration, environment);
        services.AddPlatformHealthChecks(configuration);
        services.AddOutputCache();
        services.AddYamlLocalization();
        services.AddScoped<IMessageFactory, MessageFactory>();
        services.AddScoped<CustomerSupport.Application.Notifications.IRealTimeNotifier, CustomerSupport.Api.Shared.Notifications.RealTimeNotifier>();

        return services;
    }

    private static IServiceCollection AddYamlLocalization(this IServiceCollection services)
    {
        services.AddSingleton<YamlLocalizationStore>();
        services.AddScoped<ILocalizationService, LocalizationService>();
        return services;
    }

    private static IServiceCollection AddPlatformRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("fixed", limiter =>
            {
                limiter.PermitLimit = 100;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = 10;
            });

            options.AddPolicy("login", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(5),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // AI-41 — AI routes are provider-metered, so they get their own budget. The "ai" policy
            // is the generous staff window; the external host partitions tighter windows per IP
            // through "ai-external" so a customer cannot burn the shared model budget.
            options.AddPolicy("ai", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy("ai-external", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    private static IServiceCollection AddPlatformCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins);
                }
                else
                {
                    policy.SetIsOriginAllowed(_ => environment.IsDevelopment());
                }

                policy.AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
            });
        });

        return services;
    }

    private static IServiceCollection AddPlatformHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is required");
        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";

        services.AddHealthChecks()
            .AddSqlServer(connectionString, tags: new[] { "db", "ready" })
            .AddRedis(redisConnection, tags: new[] { "cache", "ready" });

        return services;
    }
}

using CustomerSupport.Application.Ai;
using CustomerSupport.Application.Common.Options;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Entities.Identity;
using CustomerSupport.Domain.Interfaces;
using CustomerSupport.Infrastructure.ExternalApis.Providers;
using CustomerSupport.Infrastructure.ExternalApis;
using CustomerSupport.Infrastructure.Ai;
using CustomerSupport.Infrastructure.Jobs;
using CustomerSupport.Infrastructure.Messaging;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Infrastructure.Security;
using CustomerSupport.Infrastructure.Seeders;
using CustomerSupport.Infrastructure.Sla;
using CustomerSupport.Infrastructure.Services;
using CustomerSupport.Infrastructure.Storage;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Sockets;
using Polly;
using System.Reflection;

namespace CustomerSupport.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterPlatformInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddHttpClient<ICmsErpClient, CmsErpClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddAutoMapper(Assembly.GetExecutingAssembly());
        services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDistributedCacheService, DistributedCacheService>();
        services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();
        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<IIdentityUserService, IdentityUserService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ITicketReferenceGenerator, TicketReferenceGenerator>();
        services.AddFileStorage(configuration);
        services.AddHostedService<NotificationSender>();
        services.AddHostedService<LiveChatDatabaseBridge>();
        services.AddScoped<ISlaBreachScanner, SlaBreachScanner>();
        services.AddHostedService<SlaBreachDetector>();
        services.AddScoped<IBusinessHoursCalculator, BusinessHoursCalculator>();
        services.AddScoped<IEscalationLevelProvider, EscalationLevelProvider>();
        services.ConfigureIdentity();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IDbExceptionTranslator, DbExceptionTranslator>();
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IPermissionAdministrationService, PermissionAdministrationService>();
        services.AddScoped<PermissionSeeder>();
        services.AddScoped<CategorySeeder>();
        services.AddScoped<DepartmentBranchSeeder>();
        services.AddScoped<TeamSeeder>();
        services.AddScoped<EscalationLevelSeeder>();
        services.AddScoped<QuickReplySeeder>();
        services.AddScoped<ContentCategorySeeder>();
        services.AddScoped<ContentSeeder>();
        services.AddSingleton<IExternalApiConfigurationProvider, DatabaseExternalApiProvider>();
        services.AddSingleton<IDateTimeService, DateTimeService>();
        
        services.ConfigureMessaging(configuration);
        services.AddAiAssist(configuration);

        // FEAT-15 — notification gateway. Infrastructure depends only on Application contracts; the
        // SignalR implementation of IRealTimeNotifier is registered in Api.Shared.
        services.AddScoped<CustomerSupport.Domain.Services.INotificationDomainService, CustomerSupport.Domain.Services.NotificationDomainService>();
        services.AddScoped<CustomerSupport.Application.Notifications.INotificationTemplateRenderer, CustomerSupport.Infrastructure.Notifications.NotificationTemplateRenderer>();
        services.AddScoped<CustomerSupport.Application.Notifications.INotificationChannelSender, CustomerSupport.Infrastructure.Notifications.EmailNotificationChannelSender>();
        services.AddScoped<CustomerSupport.Application.Notifications.INotificationChannelSender, CustomerSupport.Infrastructure.Notifications.SmsNotificationChannelSender>();
        services.AddScoped<CustomerSupport.Application.Notifications.INotificationChannelSender, CustomerSupport.Infrastructure.Notifications.WhatsAppNotificationChannelSender>();
        services.AddScoped<CustomerSupport.Application.Notifications.INotificationChannelSender, CustomerSupport.Infrastructure.Notifications.InAppNotificationChannelSender>();
        services.AddScoped<CustomerSupport.Application.Notifications.INotificationDispatcher, CustomerSupport.Infrastructure.Notifications.NotificationDispatcher>();
        services.AddScoped<CustomerSupport.Application.Notifications.INotificationGateway, CustomerSupport.Infrastructure.Notifications.NotificationGateway>();
        services.AddScoped<CustomerSupport.Application.Channels.IWebhookSignatureVerifier, CustomerSupport.Infrastructure.Channels.MetaSignatureVerifier>();

        // Profile-update OTP verification (AC-439..AC-445). The pepper is configurable; a fixed
        // fallback keeps local development working without a setting but must be overridden in prod.
        services.AddSingleton<CustomerSupport.Application.Interfaces.IOtpCodeHasher>(
            new CustomerSupport.Infrastructure.Security.OtpCodeHasher(
                configuration["Otp:HashKey"] ?? "crm-otp-shared-pepper-change-in-production"));
        services.AddScoped<CustomerSupport.Application.Interfaces.IOtpVerificationRepository, OtpVerificationRepository>();
        services.AddScoped<CustomerSupport.Application.Interfaces.IOtpCodeGenerator, CustomerSupport.Infrastructure.Security.OtpCodeGenerator>();

        return services;
    }

    /// <summary>
    /// AI provider abstraction (AI-30) — binds the multi-provider options (with legacy flat-key
    /// back-compat), registers the resilient factory and either the real feature service or the
    /// NoOp degraded mode (A2). Adapters resolve <see cref="IHttpClientFactory"/> clients lazily,
    /// one named client per provider.
    /// </summary>
    public static IServiceCollection AddAiAssist(this IServiceCollection services, IConfiguration configuration)
    {
        var ai = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        ai.ConfigureLegacyFallback(
            configuration["Ai:BaseUrl"],
            configuration["Ai:ApiKey"],
            configuration["Ai:Model"],
            AiOptions.DefaultTimeoutSeconds);

        services.AddSingleton(ai);
        services.AddHttpClient("Ai");
        services.AddScoped<AiProviderFactory>();

        if (ai.IsAvailable)
        {
            services.AddScoped<IAiService, Ai.ResilientAiService>();
        }
        else
        {
            services.AddScoped<IAiService, Ai.NoOpAiService>();
        }

        return services;
    }

    /// <summary>
    /// Binds the storage policy and wires the local implementation of <c>IFileStore</c> (A18).
    ///
    /// <b>FileStorageOptions is registered as a singleton instance rather than through
    /// IOptions&lt;T&gt;</b>: the upload handler needs the limits to refuse a file before the stream
    /// is consumed, it lives in Application, and Application does not carry the Options package.
    /// A plain instance is one registration and no new dependency.
    ///
    /// The root defaults to <c>App_Data/attachments</c> beside the binaries — outside the web root,
    /// which is what makes AC-26's session check the only way to reach a file.
    /// </summary>
    public static IServiceCollection AddFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new FileStorageOptions();
        configuration.GetSection(FileStorageOptions.SectionName).Bind(options);

        services.AddSingleton(options);
        services.AddSingleton<IFileStore, LocalFileStore>();

        return services;
    }

    public static IServiceCollection ConfigureIdentity(this IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.Password.RequiredUniqueChars = 3;

                options.User.RequireUniqueEmail = true;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedPhoneNumber = false;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval = TimeSpan.FromMinutes(5);
        });

        return services;
    }

    public static IServiceCollection ConfigureMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var rabbitMqHost = configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitMqUsername = configuration["RabbitMQ:Username"];
        var rabbitMqPassword = configuration["RabbitMQ:Password"];
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"];
        var isDevelopment = string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase);
        var credentialsConfigured =
            !string.IsNullOrWhiteSpace(rabbitMqUsername) &&
            !rabbitMqUsername.Contains("__SET_") &&
            !string.IsNullOrWhiteSpace(rabbitMqPassword) &&
            !rabbitMqPassword.Contains("__SET_") &&
            !(rabbitMqUsername == "guest" && rabbitMqPassword == "guest");
        var messagingRequired = configuration.GetValue<bool?>("Messaging:Required") ?? !isDevelopment;
        var useInMemory = configuration.GetValue<bool>("Messaging:UseInMemory") ||
            (isDevelopment && !CanReachRabbitMq(rabbitMqHost));

        // The consumer's idempotency guard is process-local and cheap; register it regardless of
        // whether a bus is present so the consumer can resolve it whenever it is activated.
        services.AddSingleton<ChatMessagePushedDeduplicator>();

        if (!credentialsConfigured && !useInMemory)
        {
            if (messagingRequired)
            {
                throw new InvalidOperationException(
                    "RabbitMQ credentials must be configured. " +
                    "Set RABBITMQ_USERNAME and RABBITMQ_PASSWORD environment variables " +
                    "or configure RabbitMQ:Username and RabbitMQ:Password in settings.");
            }

            services.AddScoped<IMessagePublisher, NoOpMessagePublisher>();
            return services;
        }

        services.AddMassTransit(x =>
        {
            x.AddConsumer<NotificationMessageConsumer>();
            x.AddConsumer<EmailMessageConsumer>();
            x.AddConsumer<SmsMessageConsumer>();
            x.AddConsumer<ChatMessagePushedConsumer>();

            if (useInMemory)
            {
                x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
            }
            else
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(rabbitMqHost, h =>
                    {
                        h.Username(rabbitMqUsername);
                        h.Password(rabbitMqPassword);
                    });

                    cfg.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromMilliseconds(100),
                        TimeSpan.FromMilliseconds(500),
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(5)
                    ));

                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        services.AddScoped<IMessagePublisher, MassTransitMessagePublisher>();

        return services;
    }

    private static bool CanReachRabbitMq(string host)
    {
        try
        {
            using var client = new TcpClient();
            var connection = client.ConnectAsync(host, 5672);
            return connection.Wait(TimeSpan.FromMilliseconds(250)) && client.Connected;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}

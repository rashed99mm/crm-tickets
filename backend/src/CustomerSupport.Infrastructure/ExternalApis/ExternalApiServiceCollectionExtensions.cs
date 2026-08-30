using System.Reflection;
using CustomerSupport.Application.ExternalApis;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Infrastructure.ExternalApis.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Refit;

namespace CustomerSupport.Infrastructure.ExternalApis;

public static class ExternalApiServiceCollectionExtensions
{
    public static IServiceCollection AddExternalRefitClient<TClient>(
        this IServiceCollection services,
        string apiName,
        ILoggerFactory? loggerFactory = null)
        where TClient : class
    {
        var refitSettings = new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })
        };

        var builder = services.AddRefitClient<TClient>(refitSettings)
            .ConfigureHttpClient((sp, client) =>
            {
                var provider = sp.GetRequiredService<IExternalApiConfigurationProvider>();
                var config = provider.GetConfig(apiName);
                if (config != null)
                {
                    client.BaseAddress = new Uri(config.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds > 0 ? config.TimeoutSeconds : 30);
                }
            })
            .AddHttpMessageHandler(sp =>
            {
                var provider = sp.GetRequiredService<IExternalApiConfigurationProvider>();
                var config = provider.GetConfig(apiName);
                if (config?.Auth != null && config.Auth.Type != ExternalApiAuthType.None)
                {
                    var handler = ExternalApiAuthHandlerFactory.Create(config.Auth, sp.GetService<ILoggerFactory>());
                    if (handler != null)
                        return handler;
                }

                return new NoOpDelegatingHandler();
            });

        builder.AddStandardResilienceHandler();

        return services;
    }

    public static TClient GetExternalApiClient<TClient>(this IServiceProvider services)
        where TClient : class
    {
        return services.GetRequiredService<TClient>();
    }

    public static IServiceCollection AddExternalApiServices(
        this IServiceCollection services,
        IEnumerable<Assembly>? assemblies = null,
        ILoggerFactory? loggerFactory = null)
    {
        assemblies ??= GetExternalApiAssemblies();

        var clientInterfaces = DiscoverExternalApiClients(assemblies);

        foreach (var (interfaceType, apiName) in clientInterfaces)
        {
            RegisterRefitClient(services, interfaceType, apiName, loggerFactory);
        }

        return services;
    }

    private static IEnumerable<Assembly> GetExternalApiAssemblies()
    {
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        return loadedAssemblies.Where(a =>
            a.FullName?.Contains("CustomerSupport") == true &&
            !a.FullName.Contains("test", StringComparison.OrdinalIgnoreCase));
    }

    private static List<(Type interfaceType, string apiName)> DiscoverExternalApiClients(IEnumerable<Assembly> assemblies)
    {
        var clients = new List<(Type, string)>();

        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => t.IsInterface &&
                               t.GetCustomAttribute<ExternalApiClientAttribute>() != null);

                foreach (var type in types)
                {
                    var attr = type.GetCustomAttribute<ExternalApiClientAttribute>();
                    if (attr != null)
                    {
                        clients.Add((type, attr.ApiName));
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
            }
        }

        return clients;
    }

    private static IServiceCollection RegisterRefitClient(
        IServiceCollection services,
        Type clientInterface,
        string apiName,
        ILoggerFactory? loggerFactory)
    {
        var method = typeof(ExternalApiServiceCollectionExtensions)
            .GetMethod(nameof(AddExternalRefitClientGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(clientInterface);

        return (IServiceCollection)method.Invoke(null,
            new object[] { services, apiName, loggerFactory })!;
    }

    private static IServiceCollection AddExternalRefitClientGeneric<TClient>(
        IServiceCollection services,
        string apiName,
        ILoggerFactory? loggerFactory)
        where TClient : class
    {
        return services.AddExternalRefitClient<TClient>(apiName, loggerFactory);
    }
}

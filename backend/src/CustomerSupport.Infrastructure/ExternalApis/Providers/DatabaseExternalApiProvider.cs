using System.Collections.Concurrent;
using CustomerSupport.Application.ExternalApis.DTOs;
using CustomerSupport.Application.Interfaces;
using CustomerSupport.Domain.Entities.ExternalApis;
using CustomerSupport.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CustomerSupport.Infrastructure.ExternalApis.Providers;

public class DatabaseExternalApiProvider : IExternalApiConfigurationProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseExternalApiProvider> _logger;
    private ConcurrentDictionary<string, ExternalApiConfig> _configs = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    public DatabaseExternalApiProvider(IServiceScopeFactory scopeFactory, ILogger<DatabaseExternalApiProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public ExternalApiConfig? GetConfig(string apiName)
    {
        if (!_loaded)
        {
            _logger.LogWarning("External API configs not yet loaded, requesting sync load");
            LoadSync();
        }

        _configs.TryGetValue(apiName, out var config);
        return config;
    }

    public IReadOnlyList<ExternalApiConfig> GetAllConfigs()
    {
        if (!_loaded)
            LoadSync();

        return _configs.Values.ToList().AsReadOnly();
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<ExternalApiConfiguration>>();
        var secretProtector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        var entities = await repository.ListAsync(e => e.IsEnabled && !e.IsDeleted, ct);

        var newConfigs = new ConcurrentDictionary<string, ExternalApiConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in entities)
        {
            try
            {
                newConfigs[entity.Name] = MapToConfig(entity, secretProtector);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to map config for {ApiName}", entity.Name);
            }
        }

        _configs = newConfigs;
        _loaded = true;
        _logger.LogInformation("Reloaded {Count} external API configurations from database", _configs.Count);
    }

    public void LoadSync()
    {
        try
        {
            ReloadAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load external API configs synchronously");
            _loaded = true;
        }
    }

    private static ExternalApiConfig MapToConfig(ExternalApiConfiguration entity, ISecretProtector secretProtector)
    {
        var config = new ExternalApiConfig
        {
            BaseUrl = entity.BaseUrl,
            TimeoutSeconds = entity.TimeoutSeconds,
            Auth = new ExternalApiAuthConfig
            {
                Type = Enum.TryParse<ExternalApiAuthType>(entity.AuthType, out var authType) ? authType : ExternalApiAuthType.None,
                KeyName = entity.AuthKeyName ?? string.Empty,
                KeyLocation = entity.AuthKeyLocation ?? "Header",
                Value = Decrypt(entity.AuthValue, secretProtector),
                Token = Decrypt(entity.AuthToken, secretProtector),
                TokenUrl = entity.AuthTokenUrl ?? string.Empty,
                ClientId = Decrypt(entity.AuthClientId, secretProtector),
                ClientSecret = Decrypt(entity.AuthClientSecret, secretProtector),
                Scope = entity.AuthScope ?? string.Empty,
                AutoRefresh = entity.AuthAutoRefresh
            }
        };

        return config;
    }

    private static string Decrypt(string? encrypted, ISecretProtector secretProtector)
    {
        if (string.IsNullOrEmpty(encrypted))
            return string.Empty;

        try
        {
            return secretProtector.Unprotect(encrypted);
        }
        catch
        {
            return string.Empty;
        }
    }
}

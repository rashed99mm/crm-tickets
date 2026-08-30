using CustomerSupport.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace CustomerSupport.Infrastructure.Services;

/// <summary>
/// Redis-backed implementation of <see cref="IDistributedCacheService"/>.
/// Wraps <see cref="IDistributedCache"/> for standard operations and uses
/// <see cref="IConnectionMultiplexer"/> for prefix-based removal.
/// </summary>
public class DistributedCacheService : IDistributedCacheService
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly ILogger<DistributedCacheService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCacheService"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache.</param>
    /// <param name="multiplexer">The Redis connection multiplexer.</param>
    /// <param name="logger">The logger.</param>
    public DistributedCacheService(
        IDistributedCache cache,
        IConnectionMultiplexer multiplexer,
        ILogger<DistributedCacheService> logger)
    {
        _cache = cache;
        _multiplexer = multiplexer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var data = await _cache.GetStringAsync(key, ct);
        if (data == null)
        {
            _logger.LogDebug("Cache miss for key {Key}", key);
            return default;
        }

        _logger.LogDebug("Cache hit for key {Key}", key);
        return JsonSerializer.Deserialize<T>(data);
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry };
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(value), options, ct);
        _logger.LogDebug("Cached key {Key} with absolute expiry {Expiry}", key, expiry);
    }

    /// <inheritdoc />
    public async Task SetSlidingAsync<T>(string key, T value, TimeSpan slidingExpiration, CancellationToken ct = default)
    {
        var options = new DistributedCacheEntryOptions { SlidingExpiration = slidingExpiration };
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(value), options, ct);
        _logger.LogDebug("Cached key {Key} with sliding expiry {Expiry}", key, slidingExpiration);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(key, ct);
        _logger.LogDebug("Removed cache key {Key}", key);
    }

    /// <inheritdoc />
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        var endpoint = _multiplexer.GetEndPoints().FirstOrDefault();
        if (endpoint == null)
        {
            _logger.LogWarning("No Redis endpoints available for prefix removal");
            return;
        }

        var server = _multiplexer.GetServer(endpoint);
        var db = _multiplexer.GetDatabase();

        // StackExchange.Redis uses SCAN behind the scenes with pageSize to avoid blocking.
        var keys = server.Keys(pattern: $"{prefix}*", pageSize: 1000).ToArray();
        if (keys.Length == 0)
        {
            _logger.LogDebug("No keys found for prefix {Prefix}", prefix);
            return;
        }

        await db.KeyDeleteAsync(keys);
        _logger.LogInformation("Removed {Count} cache keys for prefix {Prefix}", keys.Length, prefix);
    }

    /// <inheritdoc />
    public async Task RefreshAsync(string key, CancellationToken ct = default)
    {
        await _cache.RefreshAsync(key, ct);
        _logger.LogDebug("Refreshed cache key {Key}", key);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        var data = await _cache.GetStringAsync(key, ct);
        return data != null;
    }

    /// <inheritdoc />
    public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null)
            return cached;

        var value = await factory();
        if (value is not null)
            await SetAsync(key, value, expiry, ct);

        return value;
    }
}

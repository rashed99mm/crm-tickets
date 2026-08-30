namespace CustomerSupport.Application.Interfaces;

/// <summary>
/// Abstraction over distributed caching (e.g., Redis) for storing and retrieving
/// serialized objects with configurable expiration policies.
/// </summary>
public interface IDistributedCacheService
{
    /// <summary>
    /// Retrieves a cached value by key.
    /// </summary>
    /// <typeparam name="T">The expected type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized value, or default if not found.</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Stores a value in the cache with optional absolute expiration.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="expiry">Optional absolute expiration relative to now.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);

    /// <summary>
    /// Stores a value in the cache with sliding expiration.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="slidingExpiration">Sliding expiration interval.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetSlidingAsync<T>(string key, T value, TimeSpan slidingExpiration, CancellationToken ct = default);

    /// <summary>
    /// Removes a single cache entry.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Removes all cache entries whose keys start with the given prefix.
    /// Uses Redis SCAN to avoid blocking the server.
    /// </summary>
    /// <param name="prefix">The key prefix to match.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);

    /// <summary>
    /// Refreshes the sliding expiration of a cache entry.
    /// </summary>
    /// <param name="key">The cache key to refresh.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RefreshAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Checks whether a cache entry exists.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the key exists; otherwise false.</returns>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Gets the cached value if it exists; otherwise creates it using the factory,
    /// stores it, and returns it.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">Factory to create the value on cache miss.</param>
    /// <param name="expiry">Optional absolute expiration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cached or newly created value.</returns>
    Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default);
}

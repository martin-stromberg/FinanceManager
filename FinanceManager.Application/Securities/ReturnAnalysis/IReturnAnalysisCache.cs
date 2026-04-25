namespace FinanceManager.Application.Securities.ReturnAnalysis;

/// <summary>
/// Abstraction for return analysis result caching. Allows future switch to distributed cache.
/// Cache key prefix scheme: "ra:{type}:{securityId}:{userId}"
/// </summary>
public interface IReturnAnalysisCache
{
    /// <summary>
    /// Returns a cached value or creates it via the factory and caches it.
    /// </summary>
    /// <typeparam name="T">Type of the cached value. Must be a reference type.</typeparam>
    /// <param name="cacheKey">Unique cache key for this entry.</param>
    /// <param name="factory">Async factory invoked on cache miss to produce the value.</param>
    /// <param name="ttl">Time-to-live for the cache entry.</param>
    /// <returns>Cached or freshly computed value, or null when the factory returns null.</returns>
    Task<T?> GetOrCreateAsync<T>(string cacheKey, Func<Task<T?>> factory, TimeSpan ttl) where T : class;

    /// <summary>
    /// Invalidates all cache entries starting with the given key prefix.
    /// Example: "ra:summary:{securityId}:{userId}" invalidates summary only.
    ///          "ra:{securityId}:{userId}" invalidates all entries for that security/user pair.
    /// </summary>
    /// <param name="cacheKeyPrefix">Prefix of the cache keys to invalidate.</param>
    /// <returns>A task that completes when invalidation is done.</returns>
    Task InvalidateAsync(string cacheKeyPrefix);
}

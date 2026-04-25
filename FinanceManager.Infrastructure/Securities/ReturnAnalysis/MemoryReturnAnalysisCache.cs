using System.Collections.Concurrent;
using FinanceManager.Application.Securities.ReturnAnalysis;
using Microsoft.Extensions.Caching.Memory;

namespace FinanceManager.Infrastructure.Securities.ReturnAnalysis;

/// <summary>
/// In-memory implementation of <see cref="IReturnAnalysisCache"/> using <see cref="IMemoryCache"/>.
/// Keys are tracked in a <see cref="ConcurrentDictionary{TKey,TValue}"/> for token-based invalidation.
/// Cache entries have a configurable TTL (default: 1 hour).
/// </summary>
public sealed class MemoryReturnAnalysisCache : IReturnAnalysisCache
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of <see cref="MemoryReturnAnalysisCache"/>.
    /// </summary>
    /// <param name="cache">The shared memory cache instance.</param>
    public MemoryReturnAnalysisCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<T?> GetOrCreateAsync<T>(string cacheKey, Func<Task<T?>> factory, TimeSpan ttl) where T : class
    {
        if (_cache.TryGetValue(cacheKey, out T? cached))
        {
            return cached;
        }

        var value = await factory();
        if (value != null)
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
                Size = 1
            };
            _cache.Set(cacheKey, value, options);
            _keys.TryAdd(cacheKey, 0);
        }

        return value;
    }

    /// <inheritdoc />
    public Task InvalidateAsync(string cacheKeyPrefix)
    {
        // Support both prefix-based and substring-based invalidation.
        // When the prefix contains ':' and doesn't start with "ra:", treat it as a token
        // (i.e., a substring that appears in all relevant keys).
        var toRemove = _keys.Keys
            .Where(k => k.Contains(cacheKeyPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in toRemove)
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}

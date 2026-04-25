using FinanceManager.Infrastructure.Securities.ReturnAnalysis;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace FinanceManager.Tests.Securities;

/// <summary>
/// Tests for <see cref="MemoryReturnAnalysisCache"/> covering cache miss, cache hit and invalidation behaviour.
/// </summary>
public sealed class ReturnAnalysisCacheTests : IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly MemoryReturnAnalysisCache _sut;

    /// <summary>Initializes a fresh cache instance for each test.</summary>
    public ReturnAnalysisCacheTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1_000 });
        _sut = new MemoryReturnAnalysisCache(_memoryCache);
    }

    /// <inheritdoc/>
    public void Dispose() => _memoryCache.Dispose();

    // ──────────────────────────────────────────────────────────────────────────
    // GetOrCreateAsync
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// On a cache miss the factory delegate is called and its result is returned.
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_Should_CallFactory_On_CacheMiss()
    {
        // Arrange
        const string key = "ra:test:miss";
        int callCount = 0;
        Task<string?> Factory()
        {
            callCount++;
            return Task.FromResult<string?>("value");
        }

        // Act
        string? result = await _sut.GetOrCreateAsync(key, Factory, TimeSpan.FromMinutes(5));

        // Assert
        result.Should().Be("value");
        callCount.Should().Be(1);
    }

    /// <summary>
    /// On a cache hit the factory is NOT called a second time; the cached value is returned.
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_Should_ReturnCachedValue_On_CacheHit()
    {
        // Arrange
        const string key = "ra:test:hit";
        int callCount = 0;
        Task<string?> Factory()
        {
            callCount++;
            return Task.FromResult<string?>("cached-value");
        }

        // Act: populate the cache
        await _sut.GetOrCreateAsync(key, Factory, TimeSpan.FromMinutes(5));

        // Act: second retrieval should use the cache
        string? second = await _sut.GetOrCreateAsync(key, Factory, TimeSpan.FromMinutes(5));

        // Assert
        second.Should().Be("cached-value");
        callCount.Should().Be(1, because: "the factory must only be called once on a cache hit");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // InvalidateAsync
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// InvalidateAsync removes all cached entries whose key contains the given prefix substring.
    /// After invalidation, the next call to GetOrCreateAsync triggers the factory again.
    /// </summary>
    [Fact]
    public async Task InvalidateAsync_Should_RemoveCachedEntries_WithMatchingPrefix()
    {
        // Arrange
        const string keyA = "ra:security:abc:summary";
        const string keyB = "ra:security:abc:details";
        int callCountA = 0;
        int callCountB = 0;

        await _sut.GetOrCreateAsync(keyA, () => { callCountA++; return Task.FromResult<string?>("a"); }, TimeSpan.FromMinutes(5));
        await _sut.GetOrCreateAsync(keyB, () => { callCountB++; return Task.FromResult<string?>("b"); }, TimeSpan.FromMinutes(5));

        // Act: invalidate all entries containing the security token
        await _sut.InvalidateAsync("ra:security:abc");

        // Re-request: factory must be called again because the entry was evicted
        await _sut.GetOrCreateAsync(keyA, () => { callCountA++; return Task.FromResult<string?>("a2"); }, TimeSpan.FromMinutes(5));
        await _sut.GetOrCreateAsync(keyB, () => { callCountB++; return Task.FromResult<string?>("b2"); }, TimeSpan.FromMinutes(5));

        // Assert: each factory called twice (once before, once after invalidation)
        callCountA.Should().Be(2, because: "keyA should have been evicted by the prefix invalidation");
        callCountB.Should().Be(2, because: "keyB should have been evicted by the prefix invalidation");
    }

    /// <summary>
    /// InvalidateAsync must NOT remove entries whose key does not contain the given prefix.
    /// Entries for a different security remain in the cache.
    /// </summary>
    [Fact]
    public async Task InvalidateAsync_Should_NotRemoveEntries_WithDifferentPrefix()
    {
        // Arrange
        const string keyTarget  = "ra:security:target:summary";
        const string keyOther   = "ra:security:other:summary";
        int callCountTarget = 0;
        int callCountOther  = 0;

        await _sut.GetOrCreateAsync(keyTarget, () => { callCountTarget++; return Task.FromResult<string?>("t"); }, TimeSpan.FromMinutes(5));
        await _sut.GetOrCreateAsync(keyOther,  () => { callCountOther++;  return Task.FromResult<string?>("o"); }, TimeSpan.FromMinutes(5));

        // Act: invalidate only the "target" entries
        await _sut.InvalidateAsync("ra:security:target");

        // Re-request both
        await _sut.GetOrCreateAsync(keyTarget, () => { callCountTarget++; return Task.FromResult<string?>("t2"); }, TimeSpan.FromMinutes(5));
        await _sut.GetOrCreateAsync(keyOther,  () => { callCountOther++;  return Task.FromResult<string?>("o2"); }, TimeSpan.FromMinutes(5));

        // Assert
        callCountTarget.Should().Be(2, because: "target entry was invalidated and factory must be called again");
        callCountOther.Should().Be(1,  because: "other entry was NOT invalidated and should still be cached");
    }
}

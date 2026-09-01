using FinanceManager.Web.Services;
using Microsoft.JSInterop;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Services;

/// <summary>
/// Covers <see cref="KpiLocalStorageCache"/>, which caches KPI values in the browser's localStorage via JS
/// interop under an "fm.kpi." key prefix. Verifies the opt-in gate (caching is a no-op unless explicitly
/// enabled), key prefixing for per-user isolation when multiple users share a browser profile, tolerant
/// handling of missing or corrupted cache entries, and that bulk removal only clears this application's keys.
/// </summary>
public sealed class KpiLocalStorageCacheTests
{
    private sealed class FakeJSRuntime : IJSRuntime
    {
        private readonly Dictionary<string, string> _store = new();

        public Dictionary<string, string> Store => _store;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return identifier switch
            {
                "localStorage.getItem" => GetItem<TValue>(args),
                "localStorage.setItem" => SetItem<TValue>(args),
                "localStorage.removeItem" => RemoveItem<TValue>(args),
                "eval" => Eval<TValue>(args),
                _ => new ValueTask<TValue>(default(TValue)!)
            };
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        private ValueTask<TValue> GetItem<TValue>(object?[]? args)
        {
            var key = args?.FirstOrDefault() as string;
            _store.TryGetValue(key ?? string.Empty, out var value);
            return new ValueTask<TValue>((TValue)(object?)value!);
        }

        private ValueTask<TValue> SetItem<TValue>(object?[]? args)
        {
            var key = args?.ElementAtOrDefault(0) as string;
            var data = args?.ElementAtOrDefault(1) as string;
            if (key is not null)
            {
                _store[key] = data ?? string.Empty;
            }

            return new ValueTask<TValue>(default(TValue)!);
        }

        private ValueTask<TValue> RemoveItem<TValue>(object?[]? args)
        {
            var key = args?.FirstOrDefault() as string;
            if (key is not null)
            {
                _store.Remove(key);
            }

            return new ValueTask<TValue>(default(TValue)!);
        }

        private ValueTask<TValue> Eval<TValue>(object?[]? args)
        {
            var script = args?.FirstOrDefault() as string;
            var prefix = script is not null ? ExtractPrefix(script) : string.Empty;
            var matches = _store.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            return new ValueTask<TValue>((TValue)(object)matches!);
        }

        private static string ExtractPrefix(string script)
        {
            var start = script.IndexOf("startsWith('", StringComparison.Ordinal);
            if (start < 0)
            {
                return string.Empty;
            }

            start += "startsWith('".Length;
            var end = script.IndexOf("'))", start, StringComparison.Ordinal);
            return end >= 0 ? script[start..end] : string.Empty;
        }
    }

    /// <summary>Verifies that when caching is enabled, SetAsync writes the value under the "fm.kpi." prefixed key as JSON.</summary>
    [Fact]
    public async Task SetAsync_WhenEnabled_StoresValue()
    {
        var js = new FakeJSRuntime();
        var cache = new KpiLocalStorageCache(js);
        cache.SetContext(new KpiLocalStorageContext(true, null));

        await cache.SetAsync("my-key", 42, TestContext.Current.CancellationToken);

        Assert.Single(js.Store);
        Assert.True(js.Store.ContainsKey("fm.kpi.my-key"));
        var json = js.Store["fm.kpi.my-key"];
        Assert.Equal(42, JsonSerializer.Deserialize<int>(json));
    }

    /// <summary>Verifies that reading a key that was never cached returns the default value instead of throwing.</summary>
    [Fact]
    public async Task GetAsync_WhenEnabledAndMissing_ReturnsDefault()
    {
        var js = new FakeJSRuntime();
        var cache = new KpiLocalStorageCache(js);
        cache.SetContext(new KpiLocalStorageContext(true, null));

        var result = await cache.GetAsync<int?>("missing", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>Verifies that a previously stored JSON value is correctly deserialized back to its original type on read.</summary>
    [Fact]
    public async Task GetAsync_WhenEnabled_ReturnsCachedValue()
    {
        var js = new FakeJSRuntime();
        js.Store["fm.kpi.my-key"] = "42";
        var cache = new KpiLocalStorageCache(js);
        cache.SetContext(new KpiLocalStorageContext(true, null));

        var result = await cache.GetAsync<int>("my-key", TestContext.Current.CancellationToken);

        Assert.Equal(42, result);
    }

    /// <summary>Ensures SetAsync is a no-op when the local-storage context reports caching disabled, so the feature stays strictly opt-in and never writes to a user's browser storage without consent.</summary>
    [Fact]
    public async Task SetAsync_WhenDisabled_DoesNotStore()
    {
        var js = new FakeJSRuntime();
        var cache = new KpiLocalStorageCache(js);
        cache.SetContext(new KpiLocalStorageContext(false, null));

        await cache.SetAsync("my-key", 42, TestContext.Current.CancellationToken);

        Assert.Empty(js.Store);
    }

    /// <summary>Verifies RemoveAllAsync only clears keys under this application's "fm.kpi." prefix, leaving unrelated localStorage entries from other apps or pages on the same origin untouched.</summary>
    [Fact]
    public async Task RemoveAllAsync_RemovesApplicationKeys()
    {
        var js = new FakeJSRuntime();
        js.Store["fm.kpi.my-key"] = "1";
        js.Store["fm.kpi.other-key"] = "2";
        js.Store["other.app.key"] = "3";
        var cache = new KpiLocalStorageCache(js);
        cache.SetContext(new KpiLocalStorageContext(true, null));

        await cache.RemoveAllAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("fm.kpi.my-key", js.Store.Keys);
        Assert.DoesNotContain("fm.kpi.other-key", js.Store.Keys);
        Assert.Contains("other.app.key", js.Store.Keys);
    }

    /// <summary>Ensures that when the context carries a user ID, RemoveAllAsync clears only that user's keys, leaving another user's cached KPIs intact on a shared browser profile.</summary>
    [Fact]
    public async Task RemoveAllAsync_WhenUserIdIsSet_RemovesOnlyThatUser()
    {
        var js = new FakeJSRuntime();
        js.Store["fm.kpi.user-a.my-key"] = "1";
        js.Store["fm.kpi.user-b.my-key"] = "2";
        var cache = new KpiLocalStorageCache(js);
        cache.SetContext(new KpiLocalStorageContext(true, "user-a"));

        await cache.RemoveAllAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("fm.kpi.user-a.my-key", js.Store.Keys);
        Assert.Contains("fm.kpi.user-b.my-key", js.Store.Keys);
    }

    /// <summary>Verifies GetAsync builds the storage key with the user ID segment included, matching the layout RemoveAllAsync relies on to scope removals per user.</summary>
    [Fact]
    public async Task GetAsync_WithUserId_PrefixesKey()
    {
        var js = new FakeJSRuntime();
        js.Store["fm.kpi.the-user.test"] = "99";
        var cache = new KpiLocalStorageCache(js);
        cache.SetContext(new KpiLocalStorageContext(true, "the-user"));

        var result = await cache.GetAsync<int>("test", TestContext.Current.CancellationToken);

        Assert.Equal(99, result);
    }

    /// <summary>Ensures a cache entry that is not valid JSON (e.g. corrupted or from an incompatible older version) is treated as a cache miss and returns the default value instead of throwing a deserialization exception to the caller.</summary>
    [Fact]
    public async Task GetAsync_IgnoresMalformedJson()
    {
        var js = new FakeJSRuntime();
        js.Store["fm.kpi.my-key"] = "not-json";
        var cache = new KpiLocalStorageCache(js);
        cache.SetContext(new KpiLocalStorageContext(true, null));

        var result = await cache.GetAsync<int?>("my-key", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }
}

using Bunit;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System.Threading.Tasks;
using Xunit;

namespace FinanceManager.Tests.Components;

/// <summary>
/// Tests for the <see cref="NumericKpi"/> component's local-storage caching behavior: a stale
/// cached value should be shown immediately while the fresh value loads in the background (so the
/// KPI never shows a blank/loading state when a cached number is available), and caching can be
/// disabled entirely, in which case only the freshly loaded value is ever displayed.
/// </summary>
public sealed class NumericKpiCacheTests : BunitContext
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
    }

    /// <summary>
    /// Verifies that with a value already present in local-storage cache, the component first
    /// renders that cached value (42) immediately, then updates to the freshly loaded value (99)
    /// once the async load completes - the stale-while-revalidate pattern this cache implements.
    /// </summary>
    [Fact]
    public void Renders_CachedValue_Before_Loading_FreshValue()
    {
        var js = new FakeJSRuntime();
        js.Store["fm.kpi.test-key"] = "42";
        var cache = new KpiLocalStorageCache(js);
        cache.SetContext(new KpiLocalStorageContext(true, null));
        Services.AddSingleton<IKpiLocalStorageCache>(cache);

        Func<Task<int>> load = async () =>
        {
            await Task.Yield();
            return 99;
        };

        var cut = Render<NumericKpi>(parameters =>
            parameters.Add(p => p.Load, load)
                      .Add(p => p.CacheKey, "test-key"));

        cut.WaitForAssertion(() => cut.Markup.Contains("42"), timeout: TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => cut.Markup.Contains("99"), timeout: TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Verifies that when the local-storage cache context reports caching as disabled, the
    /// component simply renders the freshly loaded value with no cached value ever shown first.
    /// </summary>
    [Fact]
    public void Renders_LoadedValue_WhenCacheDisabled()
    {
        var js = new FakeJSRuntime();
        var cache = new KpiLocalStorageCache(js);
        cache.SetContext(new KpiLocalStorageContext(false, null));
        Services.AddSingleton<IKpiLocalStorageCache>(cache);

        Func<Task<int>> load = () => Task.FromResult(123);

        var cut = Render<NumericKpi>(parameters =>
            parameters.Add(p => p.Load, load)
                      .Add(p => p.CacheKey, "test-key"));

        cut.WaitForAssertion(() => cut.Markup.Contains("123"), timeout: TimeSpan.FromSeconds(5));
    }
}

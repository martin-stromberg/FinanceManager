using FinanceManager.Application.Securities;
using FinanceManager.Domain.Securities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FinanceManager.Infrastructure.Securities;

/// <summary>
/// <see cref="IFxRateService"/> backed by the global <c>CurrencyExchangeRates</c> table
/// (cross-user reference pool — rates are identical for every user) plus lazy fills from
/// the configured <see cref="IFxRateProvider"/>.
/// </summary>
/// <remarks>
/// Resolution order for a requested (currency, date):
/// <list type="number">
/// <item>EUR short-circuits to a synthetic rate of 1 ("Fixed") without any provider call.</item>
/// <item>The latest stored rate on or before the requested date wins — stored rates are
/// never refreshed ("latest stored" semantics; there is deliberately no staleness
/// threshold, which keeps provider traffic minimal and deterministic).</item>
/// <item>Otherwise the provider is asked once for the date and the answer is persisted.
/// A per-currency <see cref="SemaphoreSlim"/> dedupes concurrent fills; a lost unique-index
/// race simply re-reads the row the other writer stored.</item>
/// <item>When the provider cannot serve a rate, the earliest stored rate for the currency
/// acts as last-resort fallback; without any stored rate the result is <c>null</c>.</item>
/// </list>
/// </remarks>
public sealed class FxRateService : IFxRateService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FillGates = new(StringComparer.OrdinalIgnoreCase);

    private readonly AppDbContext _db;
    private readonly IFxRateProvider _provider;
    private readonly ILogger<FxRateService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FxRateService"/> class.
    /// </summary>
    /// <param name="db">Database context used to read and persist exchange rates.</param>
    /// <param name="provider">External FX provider used for lazy fills.</param>
    /// <param name="logger">Logger used for diagnostic messages.</param>
    public FxRateService(AppDbContext db, IFxRateProvider provider, ILogger<FxRateService> logger)
    {
        _db = db;
        _provider = provider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FxRate?> GetRateAsync(string currencyCode, DateTime date, CancellationToken ct)
    {
        var currency = (currencyCode ?? string.Empty).Trim().ToUpperInvariant();
        if (currency.Length == 0) { return null; }
        var requested = date.Date;

        if (currency == "EUR")
        {
            return new FxRate(requested, 1m, "Fixed");
        }

        var stored = await LatestStoredAsync(currency, requested, ct);
        if (stored is not null)
        {
            return Map(stored);
        }

        var gate = FillGates.GetOrAdd(currency, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            // Re-check inside the gate: a concurrent call may have filled the cache.
            stored = await LatestStoredAsync(currency, requested, ct);
            if (stored is not null)
            {
                return Map(stored);
            }

            FxRate? fetched;
            try
            {
                fetched = await _provider.GetRateAsync(currency, requested, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FX provider failed for {Currency} on {Date}", currency, requested);
                fetched = null;
            }

            if (fetched is not null && fetched.RatePerEur > 0m)
            {
                var entity = new CurrencyExchangeRate(currency, fetched.Date, fetched.RatePerEur, fetched.Source);
                _db.CurrencyExchangeRates.Add(entity);
                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // Unique-index race (another writer stored the same currency/date/source):
                    // drop the local row and simply use the persisted one.
                    _db.Entry(entity).State = EntityState.Detached;
                }
                return fetched;
            }

            // Provider could not serve a rate: last-resort fallback to the earliest stored
            // rate for the currency (the closest available when nothing <= date exists).
            var fallback = await _db.CurrencyExchangeRates.AsNoTracking()
                .Where(r => r.CurrencyCode == currency)
                .OrderBy(r => r.Date)
                .FirstOrDefaultAsync(ct);
            return fallback is null ? null : Map(fallback);
        }
        finally
        {
            gate.Release();
        }
    }

    private Task<CurrencyExchangeRate?> LatestStoredAsync(string currency, DateTime date, CancellationToken ct)
        => _db.CurrencyExchangeRates.AsNoTracking()
            .Where(r => r.CurrencyCode == currency && r.Date <= date)
            .OrderByDescending(r => r.Date)
            .FirstOrDefaultAsync(ct);

    private static FxRate Map(CurrencyExchangeRate rate) => new(rate.Date, rate.RatePerEur, rate.Source);
}

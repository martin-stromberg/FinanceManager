namespace FinanceManager.Application.Securities;

/// <summary>
/// Resolves foreign-exchange rates ("foreign currency per 1 EUR") with a persisted,
/// cross-user cache (<see cref="FinanceManager.Domain.Securities.CurrencyExchangeRate"/>):
/// stored rates are served first, missing rates are fetched lazily from the configured
/// <see cref="IFxRateProvider"/> once and persisted. The service never throws on provider
/// or network failure — it falls back to stored data or returns <c>null</c>.
/// </summary>
public interface IFxRateService
{
    /// <summary>
    /// Returns the rate for <paramref name="currencyCode"/> valid on <paramref name="date"/>.
    /// </summary>
    /// <param name="currencyCode">ISO-4217 currency code. "EUR" short-circuits to a synthetic rate of 1 without provider calls.</param>
    /// <param name="date">Requested rate date (date component only).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The <see cref="FxRate"/> — the latest stored rate on or before <paramref name="date"/>,
    /// a freshly fetched provider rate, or the best stored fallback — or <c>null</c> when no
    /// rate is available at all (e.g. provider unreachable and nothing stored).
    /// </returns>
    Task<FxRate?> GetRateAsync(string currencyCode, DateTime date, CancellationToken ct);
}

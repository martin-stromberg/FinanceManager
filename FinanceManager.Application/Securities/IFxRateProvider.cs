namespace FinanceManager.Application.Securities;

/// <summary>
/// External provider of foreign-exchange rates against EUR
/// ("foreign currency per 1 EUR", ECB reference-rate convention).
/// Implementations must be resilient: on configuration, network or parsing failure they
/// return <c>null</c> instead of throwing — except for cancellation.
/// </summary>
public interface IFxRateProvider
{
    /// <summary>
    /// Retrieves the most recent available rate for the given currency.
    /// </summary>
    /// <param name="currencyCode">ISO-4217 code of the foreign currency (e.g. "USD"). EUR is not expected — callers short-circuit it.</param>
    /// <param name="ct">Cancellation token used to cancel network calls.</param>
    /// <returns>The latest <see cref="FxRate"/>, or <c>null</c> when the provider cannot serve one.</returns>
    Task<FxRate?> GetLatestRateAsync(string currencyCode, CancellationToken ct);

    /// <summary>
    /// Retrieves the rate valid on the given date (providers may answer with the nearest
    /// prior business day's rate — the returned <see cref="FxRate.Date"/> is authoritative).
    /// </summary>
    /// <param name="currencyCode">ISO-4217 code of the foreign currency (e.g. "USD").</param>
    /// <param name="date">Requested rate date (date component only).</param>
    /// <param name="ct">Cancellation token used to cancel network calls.</param>
    /// <returns>The <see cref="FxRate"/> for the date, or <c>null</c> when the provider cannot serve one.</returns>
    Task<FxRate?> GetRateAsync(string currencyCode, DateTime date, CancellationToken ct);
}

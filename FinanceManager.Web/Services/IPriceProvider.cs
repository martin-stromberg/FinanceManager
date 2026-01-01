namespace FinanceManager.Web.Services;

/// <summary>
/// Provides historical price data for financial symbols.
/// Implementations fetch price series from external providers and map them to date/close pairs.
/// </summary>
public interface IPriceProvider
{
    /// <summary>
    /// Retrieves daily closing prices for the specified symbol within the requested date range.
    /// </summary>
    /// <param name="symbol">Ticker symbol to query (for example "MSFT").</param>
    /// <param name="startDateExclusive">Only dates strictly after this date are included (date portion considered).</param>
    /// <param name="endDateInclusive">Only dates up to and including this date are included (date portion considered).</param>
    /// <param name="ct">Cancellation token used to cancel network calls and processing.</param>
    /// <returns>
    /// A task that resolves to a read-only list of tuples where each tuple contains the <c>date</c> and <c>close</c> price for that date.
    /// The returned sequence is expected to be ordered by date ascending.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the provided <paramref name="ct"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the provider is not properly configured (for example missing API key).</exception>
    /// <exception cref="System.Net.Http.HttpRequestException">Thrown when underlying HTTP requests fail and are not considered transient (or after retries are exhausted).</exception>
    /// <exception cref="RequestLimitExceededException">Thrown when the upstream provider signals a rate-limit condition and further requests must be stopped.</exception>
    Task<IReadOnlyList<(DateTime date, decimal close)>> GetDailyPricesAsync(string symbol, DateTime startDateExclusive, DateTime endDateInclusive, CancellationToken ct);
}
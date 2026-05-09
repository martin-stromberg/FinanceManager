using System.Net;
using FinanceManager.Application;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Web.Services;

/// <summary>
/// Price provider implementation that retrieves daily closing prices from the AlphaVantage API.
/// It resolves an API key (per-user preferred, falling back to a shared admin key),
/// queries the AlphaVantage time series endpoint and maps results to daily close values.
/// </summary>
public sealed class AlphaVantagePriceProvider : IPriceProvider
{
    private readonly IAlphaVantageKeyResolver _keys;
    private readonly ICurrentUserService _current;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<AlphaVantage> _alphaVantageLogger;

    /// <summary>
    /// Initializes a new instance of <see cref="AlphaVantagePriceProvider"/>.
    /// </summary>
    /// <param name="keys">Resolver used to obtain per-user or shared AlphaVantage API keys.</param>
    /// <param name="current">Service that provides current user context.</param>
    /// <param name="httpFactory">Http client factory used to create clients configured for AlphaVantage.</param>
    /// <param name="alphaVantageLogger">Logger passed to the underlying <see cref="AlphaVantage"/> client.</param>
    public AlphaVantagePriceProvider(
        IAlphaVantageKeyResolver keys,
        ICurrentUserService current,
        IHttpClientFactory httpFactory,
        ILogger<AlphaVantage> alphaVantageLogger)
    {
        _keys = keys;
        _current = current;
        _httpFactory = httpFactory;
        _alphaVantageLogger = alphaVantageLogger;
    }

    /// <summary>
    /// Retrieves daily closing prices for the specified symbol within the requested date range.
    /// </summary>
    /// <param name="symbol">The ticker symbol to query (for example "MSFT").</param>
    /// <param name="startDateExclusive">Only dates strictly after this date are included (date portion considered).</param>
    /// <param name="endDateInclusive">Only dates up to and including this date are included (date portion considered).</param>
    /// <param name="ct">Cancellation token used to cancel network calls.</param>
    /// <returns>
    /// A read-only list of tuples containing the date and the closing price for that date, ordered by date ascending.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when no AlphaVantage API key is configured (neither per-user nor shared).</exception>
    /// <exception cref="PriceProviderException">Thrown when AlphaVantage returns a classified provider error.</exception>
    public async Task<IReadOnlyList<(DateTime date, decimal close)>> GetDailyPricesAsync(string symbol, DateTime startDateExclusive, DateTime endDateInclusive, CancellationToken ct)
    {
        // Benutzer-Schl�ssel bevorzugen; Fallback auf freigegebenen Admin-Schl�ssel
        var apiKey = _current.IsAuthenticated
            ? await _keys.GetForUserAsync(_current.UserId, ct)
            : await _keys.GetSharedAsync(ct);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("AlphaVantage API key not configured. Provide a user key or enable sharing for an admin key.");
        }

        var http = _httpFactory.CreateClient("AlphaVantage");
        var api = new AlphaVantage(http, apiKey, _alphaVantageLogger);

        // Resilienz: Retry bei transienten HTTP-Fehlern, kein Retry bei klassifiziertem Rate-Limit / 429
        AlphaVantage.TimeSeriesDaily? series;
        try
        {
            series = await ExecuteWithRetryAsync(
                async () => await api.GetTimeSeriesDailyAsync(symbol, ct),
                maxRetries: 3,
                initialDelayMs: 400,
                ct);
        }
        catch (PriceProviderException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            var errorClass = ex.StatusCode == HttpStatusCode.TooManyRequests
                ? PriceProviderErrorClass.RateLimit
                : PriceProviderErrorClass.TransientNetwork;
            throw new PriceProviderException(errorClass, ex.Message, "Price provider request failed after retries.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new PriceProviderException(PriceProviderErrorClass.TransientNetwork, ex.Message, "Price provider request timed out after retries.", ex);
        }

        if (series is null) { return Array.Empty<(DateTime, decimal)>(); }

        var list = new List<(DateTime date, decimal close)>();
        foreach (var (date, _, _, _, close, _) in series.Enumerate())
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) { continue; }
            if (date <= startDateExclusive.Date || date > endDateInclusive.Date) { continue; }
            list.Add((date, close));
        }
        return list.OrderBy(x => x.date).ToList();
    }

    /// <summary>
    /// Executes an asynchronous operation with simple retry semantics for transient network errors.
    /// This helper will not retry when a classified non-transient provider error is thrown by the operation.
    /// </summary>
    /// <typeparam name="T">Return type of the operation.</typeparam>
    /// <param name="operation">Asynchronous operation to execute.</param>
    /// <param name="maxRetries">Maximum number of retry attempts for transient failures.</param>
    /// <param name="initialDelayMs">Initial backoff delay in milliseconds that will be multiplied by two on each retry.</param>
    /// <param name="ct">Cancellation token used to cancel delays and the operation.</param>
    /// <returns>The operation result when successful.</returns>
    /// <exception cref="PriceProviderException">Thrown when the underlying operation indicates a classified non-transient provider error; not retried.</exception>
    /// <exception cref="HttpRequestException">Thrown when a non-transient HTTP error occurs or retries are exhausted.</exception>
    /// <exception cref="TaskCanceledException">Thrown when cancellation or timeouts occur and retries are exhausted.</exception>
    private static async Task<T?> ExecuteWithRetryAsync<T>(Func<Task<T?>> operation, int maxRetries, int initialDelayMs, CancellationToken ct)
    {
        int attempt = 0;
        var delayMs = initialDelayMs;

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (PriceProviderException ex) when (ex.ErrorClass == PriceProviderErrorClass.TransientNetwork)
            {
                attempt++;
                if (attempt > maxRetries) { throw; }
                var jitter = Random.Shared.Next(0, 150);
                await Task.Delay(delayMs + jitter, ct);
                delayMs *= 2;
                continue;
            }
            catch (PriceProviderException ex) when (ex.ErrorClass != PriceProviderErrorClass.TransientNetwork)
            {
                throw;
            }
            catch (HttpRequestException ex) when (IsTransient(ex))
            {
                attempt++;
                if (attempt > maxRetries) { throw; }
                var jitter = Random.Shared.Next(0, 150);
                await Task.Delay(delayMs + jitter, ct);
                delayMs *= 2;
                continue;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout (kein explizites Cancel) -> als transient behandeln
                attempt++;
                if (attempt > maxRetries) { throw; }
                var jitter = Random.Shared.Next(0, 150);
                await Task.Delay(delayMs + jitter, ct);
                delayMs *= 2;
                continue;
            }
        }
    }

    /// <summary>
    /// Determines whether an <see cref="HttpRequestException"/> is considered transient and eligible for retry.
    /// Transient conditions include timeouts and server-side 5xx errors. 429 (TooManyRequests) is explicitly not treated as transient.
    /// </summary>
    /// <param name="ex">The exception to evaluate.</param>
    /// <returns><c>true</c> when the exception is considered transient; otherwise <c>false</c>.</returns>
    private static bool IsTransient(HttpRequestException ex)
    {
        // Retry bei 408/5xx/Bad Gateway/Service Unavailable/Gateway Timeout; kein Retry bei 429
        if (ex.StatusCode is null) { return true; }
        return ex.StatusCode switch
        {
            HttpStatusCode.RequestTimeout => true,       // 408
            HttpStatusCode.InternalServerError => true,  // 500
            HttpStatusCode.BadGateway => true,           // 502
            HttpStatusCode.ServiceUnavailable => true,   // 503
            HttpStatusCode.GatewayTimeout => true,       // 504
            HttpStatusCode.TooManyRequests => false,     // 429 -> nicht hier retryn, AlphaVantage liefert meist 200+Note
            _ => (int)ex.StatusCode.Value >= 500         // sonstige 5xx
        };
    }
}

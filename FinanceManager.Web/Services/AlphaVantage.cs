using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Web.Services;

/// <summary>
/// Client service for interacting with the AlphaVantage quote/time-series API.
/// Encapsulates request throttling handling and returns strongly-typed time series data.
/// </summary>
public sealed class AlphaVantage
{
    private static readonly Regex ApiKeyRegex = new("(?i)(apikey=)[^&\\s]+", RegexOptions.Compiled);
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<AlphaVantage> _logger;
    private DateTime _skipRequestsUntilUtc = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of <see cref="AlphaVantage"/>.
    /// </summary>
    /// <param name="http">An <see cref="HttpClient"/> used to perform requests. Must not be <c>null</c>.</param>
    /// <param name="apiKey">AlphaVantage API key. When empty or null some operations will throw <see cref="ArgumentException"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="http"/> is <c>null</c>.</exception>
    public AlphaVantage(HttpClient http, string apiKey, ILogger<AlphaVantage>? logger = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _apiKey = apiKey;
        _logger = logger ?? NullLogger<AlphaVantage>.Instance;
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri("https://www.alphavantage.co/");
        }
    }

    /// <summary>
    /// Gets a value indicating whether the client is currently blocked from issuing further requests
    /// because a rate-limit notice was previously observed. When <c>true</c> calls should be avoided
    /// until the configured next retry time.<br/>
    /// This value is set conservatively to the start of the next UTC day when rate-limit messages are received.
    /// </summary>
    public bool LimitExceeded => DateTime.UtcNow < _skipRequestsUntilUtc;

    /// <summary>
    /// Retrieves the full daily time series for the specified symbol.
    /// </summary>
    /// <param name="symbol">The symbol (ticker) to request, e.g. "MSFT". Must not be null or whitespace.</param>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns>
    /// A <see cref="TimeSeriesDaily"/> instance containing raw bars when the symbol was found; otherwise <c>null</c>
    /// if the response did not contain a valid time series.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the configured API key or <paramref name="symbol"/> is missing/invalid.</exception>
    /// <exception cref="PriceProviderException">Thrown when AlphaVantage returns a classified provider error response.</exception>
    public async Task<TimeSeriesDaily?> GetTimeSeriesDailyAsync(string symbol, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey)) { throw new ArgumentException("API key required", nameof(_apiKey)); }
        if (string.IsNullOrWhiteSpace(symbol)) { throw new ArgumentException("symbol required", nameof(symbol)); }

        var providerCorrelationId = Guid.NewGuid().ToString("N");
        var requestPath = $"query?function=TIME_SERIES_DAILY&symbol={Uri.EscapeDataString(symbol)}&outputsize=compact&apikey={_apiKey}";
        var sanitizedRequestPath = SanitizeForLog(requestPath);
        var traceId = Activity.Current?.TraceId.ToString();
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "AlphaVantage request started. Provider={Provider} Function={Function} Symbol={Symbol} CorrelationId={CorrelationId} TraceId={TraceId} Url={Url}",
            "AlphaVantage",
            "TIME_SERIES_DAILY",
            symbol,
            providerCorrelationId,
            traceId,
            sanitizedRequestPath);

        if (LimitExceeded)
        {
            _logger.LogWarning(
                "AlphaVantage request skipped due to active rate limit. Symbol={Symbol} CorrelationId={CorrelationId} SkipUntilUtc={SkipUntilUtc}",
                symbol,
                providerCorrelationId,
                _skipRequestsUntilUtc);
            throw new PriceProviderException(
                PriceProviderErrorClass.RateLimit,
                $"AlphaVantage limit exceeded. Next attempt after {_skipRequestsUntilUtc:u}.",
                "Price provider rate limit is currently active.");
        }

        try
        {
            using var resp = await _http.GetAsync(requestPath, ct);
            _logger.LogInformation(
                "AlphaVantage response received. Symbol={Symbol} CorrelationId={CorrelationId} StatusCode={StatusCode} DurationMs={DurationMs}",
                symbol,
                providerCorrelationId,
                (int)resp.StatusCode,
                stopwatch.ElapsedMilliseconds);
            var responseBody = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                var (errorClass, responseKind, providerMessage) = ClassifyNonSuccessResponse(resp.StatusCode, responseBody);
                if (errorClass == PriceProviderErrorClass.RateLimit)
                {
                    _skipRequestsUntilUtc = DateTime.UtcNow.Date.AddDays(1);
                }

                _logger.LogWarning(
                    "AlphaVantage response classified as HTTP provider error. Symbol={Symbol} CorrelationId={CorrelationId} ErrorClass={ErrorClass} ResponseKind={ResponseKind} StatusCode={StatusCode} DurationMs={DurationMs} Detail={Detail}",
                    symbol,
                    providerCorrelationId,
                    errorClass.ToCode(),
                    responseKind,
                    (int)resp.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    providerMessage);

                throw new PriceProviderException(errorClass, providerMessage, "Price provider request failed.");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (root.TryGetProperty("Note", out var noteEl))
            {
                var note = noteEl.GetString() ?? "Rate limit reached";
                // Conservative: block until start of next UTC day
                _skipRequestsUntilUtc = DateTime.UtcNow.Date.AddDays(1);
                _logger.LogWarning(
                    "AlphaVantage response classified as rate limit. Symbol={Symbol} CorrelationId={CorrelationId} ErrorClass={ErrorClass} ResponseKind={ResponseKind} DurationMs={DurationMs} Detail={Detail}",
                    symbol,
                    providerCorrelationId,
                    PriceProviderErrorClass.RateLimit.ToCode(),
                    "Note",
                     stopwatch.ElapsedMilliseconds,
                     SanitizeForLog(note));
                throw new PriceProviderException(PriceProviderErrorClass.RateLimit, SanitizeForLog(note), "Price provider returned a rate-limit response.");
            }
            if (root.TryGetProperty("Information", out var infoEl))
            {
                var info = infoEl.GetString() ?? "Request information";
                _skipRequestsUntilUtc = DateTime.UtcNow.Date.AddDays(1);
                _logger.LogWarning(
                    "AlphaVantage response classified as rate limit. Symbol={Symbol} CorrelationId={CorrelationId} ErrorClass={ErrorClass} ResponseKind={ResponseKind} DurationMs={DurationMs} Detail={Detail}",
                    symbol,
                    providerCorrelationId,
                    PriceProviderErrorClass.RateLimit.ToCode(),
                    "Information",
                     stopwatch.ElapsedMilliseconds,
                     SanitizeForLog(info));
                throw new PriceProviderException(PriceProviderErrorClass.RateLimit, SanitizeForLog(info), "Price provider returned an informational rate-limit response.");
            }
            if (root.TryGetProperty("Error Message", out var errEl))
            {
                var msg = errEl.GetString() ?? "Unknown AlphaVantage error";
                var errorClass = IsInvalidSymbolOrFunction(msg)
                    ? PriceProviderErrorClass.InvalidSymbolOrFunction
                    : PriceProviderErrorClass.UnknownProviderError;
                _logger.LogWarning(
                    "AlphaVantage response classified as provider error. Symbol={Symbol} CorrelationId={CorrelationId} ErrorClass={ErrorClass} ResponseKind={ResponseKind} DurationMs={DurationMs} Detail={Detail}",
                    symbol,
                    providerCorrelationId,
                    errorClass.ToCode(),
                     "ErrorMessage",
                     stopwatch.ElapsedMilliseconds,
                     SanitizeForLog(msg));

                throw new PriceProviderException(errorClass, SanitizeForLog(msg), "Price provider returned an error response.");
            }

            // Rewind by re-serializing to our model
            var json = JsonSerializer.Deserialize<TimeSeriesDailyResponse>(root.GetRawText(), JsonOptions);
            if (json is null || json.TimeSeries is null)
            {
                var payloadPreview = SanitizeForLog(root.GetRawText());
                _logger.LogWarning(
                    "AlphaVantage response classified as unexpected payload. Symbol={Symbol} CorrelationId={CorrelationId} ResponseKind={ResponseKind} DurationMs={DurationMs} PayloadPreview={PayloadPreview}",
                    symbol,
                    providerCorrelationId,
                    "UnexpectedPayload",
                    stopwatch.ElapsedMilliseconds,
                    payloadPreview);
                throw new PriceProviderException(
                    PriceProviderErrorClass.UnknownProviderError,
                    payloadPreview,
                    "Price provider returned an unexpected payload.");
            }

            _logger.LogInformation(
                "AlphaVantage request succeeded. Symbol={Symbol} CorrelationId={CorrelationId} Bars={Bars} DurationMs={DurationMs}",
                symbol,
                providerCorrelationId,
                json.TimeSeries.Count,
                stopwatch.ElapsedMilliseconds);
            return new TimeSeriesDaily(json.TimeSeries);
        }
        catch (PriceProviderException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            var errorClass = ClassifyHttpTransportError(ex.StatusCode);
            if (errorClass == PriceProviderErrorClass.RateLimit)
            {
                _skipRequestsUntilUtc = DateTime.UtcNow.Date.AddDays(1);
            }

            _logger.LogWarning(
                ex,
                "AlphaVantage request failed with transport error. Symbol={Symbol} CorrelationId={CorrelationId} ErrorClass={ErrorClass} ResponseKind={ResponseKind} DurationMs={DurationMs} StatusCode={StatusCode} Error={Error}",
                symbol,
                providerCorrelationId,
                errorClass.ToCode(),
                "TransportException",
                stopwatch.ElapsedMilliseconds,
                ex.StatusCode,
                SanitizeForLog(ex.Message));
            throw new PriceProviderException(errorClass, SanitizeForLog(ex.Message), "Price provider request failed.", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "AlphaVantage response parsing failed. Symbol={Symbol} CorrelationId={CorrelationId} ErrorClass={ErrorClass} ResponseKind={ResponseKind} DurationMs={DurationMs} Error={Error}",
                symbol,
                providerCorrelationId,
                PriceProviderErrorClass.UnknownProviderError.ToCode(),
                "MalformedJson",
                stopwatch.ElapsedMilliseconds,
                SanitizeForLog(ex.Message));
            throw new PriceProviderException(
                PriceProviderErrorClass.UnknownProviderError,
                SanitizeForLog(ex.Message),
                "Price provider returned an invalid JSON payload.",
                ex);
        }
    }

    private static bool IsInvalidSymbolOrFunction(string message)
        => message.Contains("Invalid API call", StringComparison.OrdinalIgnoreCase)
           && message.Contains("TIME_SERIES_DAILY", StringComparison.OrdinalIgnoreCase);

    private static (PriceProviderErrorClass errorClass, string responseKind, string providerMessage) ClassifyNonSuccessResponse(HttpStatusCode statusCode, string responseBody)
    {
        var providerMessage = string.IsNullOrWhiteSpace(responseBody)
            ? $"HTTP {(int)statusCode} {statusCode}"
            : SanitizeForLog(responseBody);

        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            return (PriceProviderErrorClass.RateLimit, "Http429", providerMessage);
        }

        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;
                if (root.TryGetProperty("Note", out var noteEl))
                {
                    return (PriceProviderErrorClass.RateLimit, "Note", SanitizeForLog(noteEl.GetString() ?? providerMessage));
                }

                if (root.TryGetProperty("Information", out var infoEl))
                {
                    return (PriceProviderErrorClass.RateLimit, "Information", SanitizeForLog(infoEl.GetString() ?? providerMessage));
                }

                if (root.TryGetProperty("Error Message", out var errEl))
                {
                    var errorMessage = errEl.GetString() ?? providerMessage;
                    var errorClass = IsInvalidSymbolOrFunction(errorMessage)
                        ? PriceProviderErrorClass.InvalidSymbolOrFunction
                        : PriceProviderErrorClass.UnknownProviderError;
                    return (errorClass, "ErrorMessage", SanitizeForLog(errorMessage));
                }
            }
            catch (JsonException)
            {
                // Keep status-code based fallback classification.
            }
        }

        return IsTransientHttpStatus(statusCode)
            ? (PriceProviderErrorClass.TransientNetwork, "HttpStatus", providerMessage)
            : (PriceProviderErrorClass.UnknownProviderError, "HttpStatus", providerMessage);
    }

    private static PriceProviderErrorClass ClassifyHttpTransportError(HttpStatusCode? statusCode)
        => statusCode switch
        {
            HttpStatusCode.TooManyRequests => PriceProviderErrorClass.RateLimit,
            HttpStatusCode.RequestTimeout or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout => PriceProviderErrorClass.TransientNetwork,
            null => PriceProviderErrorClass.TransientNetwork,
            _ when (int)statusCode.Value >= 500 => PriceProviderErrorClass.TransientNetwork,
            _ => PriceProviderErrorClass.UnknownProviderError
        };

    private static bool IsTransientHttpStatus(HttpStatusCode statusCode)
        => statusCode switch
        {
            HttpStatusCode.RequestTimeout or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout => true,
            _ when (int)statusCode >= 500 => true,
            _ => false
        };

    private static string SanitizeForLog(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sanitized = new string(value.Where(ch => !char.IsControl(ch) || ch is '\n' or '\r' or '\t').ToArray());
        sanitized = ApiKeyRegex.Replace(sanitized, "$1***");
        return sanitized.Length <= 500 ? sanitized : sanitized[..500];
    }

    /// <summary>
    /// Container for the parsed daily time series returned by AlphaVantage.
    /// </summary>
    public sealed class TimeSeriesDaily
    {
        /// <summary>
        /// Raw dictionary mapping date string to numeric bar values.
        /// </summary>
        public IReadOnlyDictionary<string, BarRaw> Raw { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="TimeSeriesDaily"/>.
        /// </summary>
        /// <param name="raw">Dictionary of raw bar values keyed by date string.</param>
        public TimeSeriesDaily(Dictionary<string, BarRaw> raw) { Raw = raw; }

        /// <summary>
        /// Enumerates parsed bars as tuples with strongly typed values.
        /// </summary>
        /// <returns>An enumeration of tuples containing the date and open/high/low/close/volume values.</returns>
        public IEnumerable<(DateTime date, decimal open, decimal high, decimal low, decimal close, long volume)> Enumerate()
        {
            foreach (var kv in Raw)
            {
                if (!DateTime.TryParse(kv.Key, out var d)) { continue; }
                var b = kv.Value;
                if (!TryParseDecimal(b.Open, out var open) || !TryParseDecimal(b.High, out var high) || !TryParseDecimal(b.Low, out var low) || !TryParseDecimal(b.Close, out var close) || !long.TryParse(b.Volume, out var vol))
                {
                    continue;
                }
                yield return (d.Date, open, high, low, close, vol);
            }
        }

        private static bool TryParseDecimal(string? s, out decimal value)
            => decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Internal representation of the JSON response for daily time series.
    /// </summary>
    public sealed class TimeSeriesDailyResponse
    {
        /// <summary>
        /// The time series data keyed by date string (property name is the literal returned by AlphaVantage JSON).
        /// </summary>
        [JsonPropertyName("Time Series (Daily)")]
        public Dictionary<string, BarRaw>? TimeSeries { get; set; }

        /// <summary>
        /// Optional throttle / error note returned by AlphaVantage.
        /// </summary>
        [JsonPropertyName("Note")] public string? Note { get; set; }
        /// <summary>
        /// Optional informational text returned by AlphaVantage.
        /// </summary>
        [JsonPropertyName("Information")] public string? Information { get; set; }
        /// <summary>
        /// Optional error message returned by AlphaVantage for invalid requests.
        /// </summary>
        [JsonPropertyName("Error Message")] public string? Error { get; set; }
    }

    /// <summary>
    /// Raw numeric values of a single OHLCV bar as returned by AlphaVantage.
    /// </summary>
    public sealed class BarRaw
    {
        /// <summary>
        /// Opening price as provided by AlphaVantage (string representation, invariant culture decimal format).
        /// </summary>
        [JsonPropertyName("1. open")] public string? Open { get; set; }

        /// <summary>
        /// Highest price during the period as provided by AlphaVantage (string representation).
        /// </summary>
        [JsonPropertyName("2. high")] public string? High { get; set; }

        /// <summary>
        /// Lowest price during the period as provided by AlphaVantage (string representation).
        /// </summary>
        [JsonPropertyName("3. low")] public string? Low { get; set; }

        /// <summary>
        /// Closing price as provided by AlphaVantage (string representation).
        /// </summary>
        [JsonPropertyName("4. close")] public string? Close { get; set; }

        /// <summary>
        /// Traded volume during the period as provided by AlphaVantage (string representation of integer).
        /// </summary>
        [JsonPropertyName("5. volume")] public string? Volume { get; set; }
    }

    /// <summary>
    /// JSON serializer options used to parse AlphaVantage responses.
    /// </summary>
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}


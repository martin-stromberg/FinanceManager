using FinanceManager.Application.Securities;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Web.Services;

/// <summary>
/// <see cref="IFxRateProvider"/> implementation backed by the Frankfurter API
/// (<c>https://api.frankfurter.dev</c>) — free, key-less ECB reference rates quoted as
/// "foreign currency per 1 EUR". Endpoints: <c>GET /latest?from=EUR&amp;to={cc}</c> and
/// <c>GET /{yyyy-MM-dd}?from=EUR&amp;to={cc}</c>. Transient failures get one retry; every
/// other failure (missing config, HTTP error, malformed payload) results in <c>null</c>.
/// Rates are tagged with <see cref="SourceName"/> so stored rows honestly name the
/// serving API (the underlying data are ECB reference rates).
/// </summary>
public sealed class FrankfurterFxRateProvider : IFxRateProvider
{
    /// <summary>Value stored/emitted as <see cref="FxRate.Source"/> for rates of this provider.</summary>
    public const string SourceName = "Frankfurter";

    /// <summary>Name of the configured <see cref="IHttpClientFactory"/> client.</summary>
    public const string HttpClientName = "Frankfurter";

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<FrankfurterFxRateProvider> _logger;
    private readonly FxOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="FrankfurterFxRateProvider"/>.
    /// </summary>
    /// <param name="httpFactory">Factory used to create the named "Frankfurter" HTTP client.</param>
    /// <param name="logger">Logger used for diagnostic messages.</param>
    /// <param name="options">FX configuration (base URL, enabled flag); defaults are used when not configured.</param>
    public FrankfurterFxRateProvider(IHttpClientFactory httpFactory, ILogger<FrankfurterFxRateProvider> logger, IOptions<FxOptions>? options = null)
    {
        _httpFactory = httpFactory;
        _logger = logger;
        _options = options?.Value ?? new FxOptions();
    }

    /// <inheritdoc />
    public Task<FxRate?> GetLatestRateAsync(string currencyCode, CancellationToken ct)
        => FetchAsync(currencyCode, "latest", ct);

    /// <inheritdoc />
    public Task<FxRate?> GetRateAsync(string currencyCode, DateTime date, CancellationToken ct)
        => FetchAsync(currencyCode, date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ct);

    private async Task<FxRate?> FetchAsync(string currencyCode, string path, CancellationToken ct)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(currencyCode)) { return null; }
        var currency = currencyCode.Trim().ToUpperInvariant();
        var baseUrl = (_options.BaseUrl ?? string.Empty).TrimEnd('/');
        if (baseUrl.Length == 0) { return null; }
        var url = $"{baseUrl}/{path}?from=EUR&to={Uri.EscapeDataString(currency)}";

        // One retry on transient failures (timeout / network / 5xx), mirroring the
        // AlphaVantage provider's resilience pattern in compact form.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var http = _httpFactory.CreateClient(HttpClientName);
                var payload = await http.GetFromJsonAsync<FrankfurterResponse>(url, ct);
                if (payload?.Rates is null
                    || !payload.Rates.TryGetValue(currency, out var rate)
                    || rate <= 0m
                    || !DateTime.TryParse(payload.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var rateDate))
                {
                    return null;
                }
                return new FxRate(rateDate.Date, rate, SourceName);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt == 0 && (ex is HttpRequestException or TaskCanceledException or JsonException))
            {
                _logger.LogDebug(ex, "Frankfurter FX request {Url} failed; retrying once", url);
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Frankfurter FX request {Url} failed", url);
                return null;
            }
        }
    }

    private sealed class FrankfurterResponse
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("rates")]
        public Dictionary<string, decimal>? Rates { get; set; }
    }
}

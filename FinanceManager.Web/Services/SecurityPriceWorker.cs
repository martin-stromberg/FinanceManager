using FinanceManager.Application.Securities;
using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinanceManager.Web.Services;

/// <summary>
/// Background worker that periodically fetches security prices from an external provider
/// (AlphaVantage) and persists them into the application's database.
/// </summary>
/// <remarks>
/// The worker creates a new scope per run to resolve scoped services (DbContext, update service).
/// It selects the securities and the fetch window for each run; the actual maintenance pass
/// (fetch → error state → persist → signal evaluation → notification) is executed by
/// <see cref="ISecurityPriceUpdateService"/>.
/// It respects configured quota options (<see cref="AlphaVantageQuotaOptions"/>) and handles rate limits and provider errors.
/// </remarks>
public sealed class SecurityPriceWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SecurityPriceWorker> _logger;
    private readonly IOptions<AlphaVantageQuotaOptions> _quota;

    /// <summary>
    /// Initializes a new instance of <see cref="SecurityPriceWorker"/>.
    /// </summary>
    /// <param name="scopeFactory">Factory used to create a new scope for each run (resolves scoped services).</param>
    /// <param name="logger">Logger used for diagnostic messages.</param>
    /// <param name="quota">Configuration options controlling run quotas (max symbols per run, requests per minute).</param>
    public SecurityPriceWorker(IServiceScopeFactory scopeFactory, ILogger<SecurityPriceWorker> logger, IOptions<AlphaVantageQuotaOptions> quota)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _quota = quota;
    }

    /// <summary>
    /// Main execution loop for the background service. Runs until <paramref name="stoppingToken"/> is signaled.
    /// </summary>
    /// <param name="stoppingToken">Token used to indicate service shutdown.</param>
    /// <returns>A task that completes when the background service stops.</returns>
    /// <remarks>
    /// Exceptions during a single run are logged. When a <see cref="RequestLimitExceededException"/> occurs it is
    /// logged as a warning; other exceptions are logged as errors. After each run the worker pauses for one hour.
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (RequestLimitExceededException ex)
            {
                _logger.LogWarning(ex, "AlphaVantage limit exceeded. Pausing worker run.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SecurityPriceWorker run failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // shutting down
            }
        }
    }

    /// <summary>
    /// Executes a single run: selects eligible securities and their fetch windows and executes the
    /// shared price maintenance pass (<see cref="ISecurityPriceUpdateService.RefreshFromProviderAsync"/>)
    /// for each of them.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the run and all IO operations.</param>
    /// <returns>A task that completes when the run has finished.</returns>
    /// <remarks>
    /// - Symbols without an AlphaVantage code or that are inactive are skipped.
    /// - The method respects quota settings from <see cref="AlphaVantageQuotaOptions"/>.
    /// - Provider domain errors are handled inside the pass (error flag plus owner notification).
    /// </remarks>
    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var priceUpdates = scope.ServiceProvider.GetRequiredService<ISecurityPriceUpdateService>();
        var resolver = scope.ServiceProvider.GetRequiredService<IAlphaVantageKeyResolver>();

        // Check for shared admin key configured
        var sharedKey = await resolver.GetSharedAsync(ct);
        if (string.IsNullOrWhiteSpace(sharedKey))
        {
            _logger.LogInformation("SecurityPriceWorker: No shared AlphaVantage admin key configured. Skipping run.");
            return;
        }

        var today = DateTime.UtcNow.Date;
        var endInclusive = today.AddDays(-1);
        while (endInclusive.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            endInclusive = endInclusive.AddDays(-1);
        }

        var maxSymbols = Math.Max(1, _quota.Value.MaxSymbolsPerRun);
        var batch = await db.Securities.AsNoTracking()
            .Where(s => s.IsActive && s.AlphaVantageCode != null)
            .Select(s => new
            {
                Sec = s,
                LastDate = db.SecurityPrices.Where(p => p.SecurityId == s.Id).Max(p => (DateTime?)p.Date)
            })
            .OrderBy(x => x.LastDate ?? DateTime.MinValue)
            .ThenBy(x => x.Sec.Id)
            .Take(maxSymbols)
            .ToListAsync(ct);

        if (batch.Count == 0)
        {
            _logger.LogInformation("SecurityPriceWorker: No eligible securities found for price update.");
            return;
        }

        var rpm = _quota.Value.RequestsPerMinute;
        var delayPerRequest = rpm > 0 ? TimeSpan.FromMilliseconds(Math.Ceiling(60000.0 / rpm)) : TimeSpan.Zero;

        int processed = 0, upserted = 0;
        foreach (var item in batch)
        {
            ct.ThrowIfCancellationRequested();
            var sec = item.Sec;

            try
            {
                var last = item.LastDate ?? default;
                var startExclusive = last == default ? endInclusive.AddYears(-2) : last;
                var requiresValidation = sec.HasPriceError;
                if (endInclusive <= startExclusive && !requiresValidation)
                {
                    continue;
                }

                var result = await priceUpdates.RefreshFromProviderAsync(
                    new SecurityPriceRefreshRequest(sec.OwnerUserId, sec.Id, sec.AlphaVantageCode!, sec.Name, sec.Identifier, startExclusive, endInclusive),
                    ct);

                if (result.Status == SecurityPriceRefreshStatus.Stored)
                {
                    processed++;
                    upserted += (result.StoreResult?.Inserted ?? 0) + (result.StoreResult?.Updated ?? 0);
                }
            }
            catch (RequestLimitExceededException ex)
            {
                _logger.LogWarning(ex, "AlphaVantage rate limit reached while processing {Code}. Stopping this run.", sec.AlphaVantageCode);
                break; // stop run on rate limit
            }
            catch (Exception ex)
            {
                // Unexpected error: log but don't mark security as errored (may be transient)
                _logger.LogError(ex, "Failed to update prices for security {SecurityId} ({Code})", sec.Id, sec.AlphaVantageCode);
            }

            if (delayPerRequest > TimeSpan.Zero)
            {
                try { await Task.Delay(delayPerRequest, ct); } catch (TaskCanceledException) { }
            }
        }

        _logger.LogInformation("SecurityPriceWorker: Processed {Processed} securities, upserted {Upserted} prices (limit {Limit}, rpm {Rpm}).",
            processed, upserted, maxSymbols, rpm);
    }
}

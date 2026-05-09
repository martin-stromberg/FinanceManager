using FinanceManager.Application.Notifications; // NEW
using FinanceManager.Domain.Notifications;    // NEW
using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinanceManager.Web.Services;

/// <summary>
/// Background worker that periodically fetches security prices from an external provider
/// (AlphaVantage) and persists them into the application's database.
/// </summary>
/// <remarks>
/// The worker creates a new scope per run to resolve scoped services (DbContext, providers, notification writer).
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
    /// Exceptions during a single run are logged. Classified provider failures are handled inside the run;
    /// unhandled exceptions are logged as errors. After each run the worker pauses for one hour.
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
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
    /// Executes a single run: selects eligible securities, queries prices from the provider,
    /// writes new price entries and handles provider errors (including marking securities as errored
    /// and emitting notifications to users).
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the run and all IO operations.</param>
    /// <returns>A task that completes when the run has finished.</returns>
    /// <remarks>
    /// - Symbols without an AlphaVantage code or that are inactive are skipped.
    /// - Weekend dates are ignored when persisting prices.
    /// - The method respects quota settings from <see cref="AlphaVantageQuotaOptions"/>.
    /// - Fatal provider errors (here surfaced as <see cref="InvalidOperationException"/>) mark the security as errored
    ///   and create a system notification for the owner.
    /// </remarks>
    private async Task RunOnceAsync(CancellationToken ct)
    {
        var runId = Guid.NewGuid();
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var prices = scope.ServiceProvider.GetRequiredService<IPriceProvider>();
        var notifier = scope.ServiceProvider.GetRequiredService<INotificationWriter>(); // NEW
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
            .Where(s => s.IsActive && s.AlphaVantageCode != null && !s.HasPriceError)
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

        int processed = 0, inserted = 0;
        foreach (var item in batch)
        {
            ct.ThrowIfCancellationRequested();
            var sec = item.Sec;

            try
            {
                var last = await db.SecurityPrices.AsNoTracking()
                    .Where(p => p.SecurityId == sec.Id)
                    .OrderByDescending(p => p.Date)
                    .Select(p => p.Date)
                    .FirstOrDefaultAsync(ct);

                var startExclusive = last == default ? endInclusive.AddYears(-2) : last;
                if (endInclusive <= startExclusive)
                {
                    continue;
                }

                var pricesList = await prices.GetDailyPricesAsync(sec.AlphaVantageCode!, startExclusive, endInclusive, ct);
                if (pricesList.Count == 0)
                {
                    continue;
                }

                foreach (var (date, close) in pricesList)
                {
                    if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    {
                        continue;
                    }
                    db.SecurityPrices.Add(new FinanceManager.Domain.Securities.SecurityPrice(sec.Id, date, close));
                    inserted++;
                }
                await db.SaveChangesAsync(ct);
                processed++;
            }
            catch (PriceProviderException ex)
            {
                var errorClassCode = ex.ErrorClassCode;
                _logger.LogWarning(
                    ex,
                    "Security price fetch failed. RunId={RunId} SecurityId={SecurityId} OwnerUserId={OwnerUserId} Symbol={Symbol} ErrorClass={ErrorClass}",
                    runId,
                    sec.Id,
                    sec.OwnerUserId,
                    sec.AlphaVantageCode,
                    errorClassCode);

                if (ex.ErrorClass == PriceProviderErrorClass.RateLimit)
                {
                    _logger.LogWarning("SecurityPriceWorker stopping run due to provider rate limit. RunId={RunId}", runId);
                    break;
                }

                if (ex.ErrorClass == PriceProviderErrorClass.TransientNetwork)
                {
                    continue;
                }

                var entity = await db.Securities.FirstOrDefaultAsync(s => s.Id == sec.Id, ct);
                if (entity != null)
                {
                    var userMessage = BuildUserNotificationMessage(ex.ErrorClass, sec.Name, sec.Identifier, DateTime.UtcNow);
                    entity.SetPriceError(errorClassCode, userMessage, SanitizeProviderRawMessage(ex.ProviderRawMessage));
                    await db.SaveChangesAsync(ct);

                    var title = "Kursabruf fehlgeschlagen";
                    var msg = userMessage;
                    var trigger = $"security:error:{sec.Id}";
                    await notifier.CreateForUserAsync(sec.OwnerUserId, title, msg, NotificationType.SystemAlert, NotificationTarget.HomePage, DateTime.UtcNow.Date, trigger, ct);
                }
            }
            catch (Exception ex)
            {
                // Unexpected error: log but don't mark security as errored (may be transient)
                _logger.LogError(ex, "Failed to update prices for security {SecurityId} ({Code}) in run {RunId}", sec.Id, sec.AlphaVantageCode, runId);
            }

            if (delayPerRequest > TimeSpan.Zero)
            {
                try { await Task.Delay(delayPerRequest, ct); } catch (TaskCanceledException) { }
            }
        }

        _logger.LogInformation("SecurityPriceWorker: Processed {Processed} securities, inserted {Inserted} prices (limit {Limit}, rpm {Rpm}).",
            processed, inserted, maxSymbols, rpm);
    }

    private static string BuildUserNotificationMessage(PriceProviderErrorClass errorClass, string securityName, string securityIdentifier, DateTime occurredUtc)
    {
        var occurredText = occurredUtc.ToString("yyyy-MM-dd HH:mm 'UTC'");
        return errorClass switch
        {
            PriceProviderErrorClass.InvalidSymbolOrFunction =>
                $"Für '{securityName}' ({securityIdentifier}) konnte kein Kurs geladen werden ({occurredText}). Bitte Symbol prüfen, speichern und anschließend den Abruf erneut starten.",
            PriceProviderErrorClass.UnknownProviderError =>
                $"Für '{securityName}' ({securityIdentifier}) ist beim Kursabruf ein externer Fehler aufgetreten ({occurredText}). Bitte Hinweis bestätigen und den Abruf später erneut starten.",
            _ =>
                $"Für '{securityName}' ({securityIdentifier}) ist beim Kursabruf ein Fehler aufgetreten ({occurredText})."
        };
    }

    private static string? SanitizeProviderRawMessage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var filtered = new string(value.Where(ch => !char.IsControl(ch) || ch is '\n' or '\r' or '\t').ToArray()).Trim();
        if (filtered.Length == 0)
        {
            return null;
        }
        return filtered.Length <= 2000 ? filtered : filtered[..2000];
    }
}

using FinanceManager.Application;
using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Text.Json;

namespace FinanceManager.Web.Services;

/// <summary>
/// Background task executor that backfills historical security prices using the configured price provider.
/// </summary>
public sealed class SecurityPricesBackfillExecutor : IBackgroundTaskExecutor
{
    /// <summary>
    /// The background task type handled by this executor.
    /// </summary>
    public BackgroundTaskType Type => BackgroundTaskType.SecurityPricesBackfill;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SecurityPricesBackfillExecutor> _logger;
    private readonly IStringLocalizer _localizer;

    private sealed record Payload(Guid? SecurityId, DateTime? FromDateUtc, DateTime? ToDateUtc);

    /// <summary>
    /// Initializes a new instance of <see cref="SecurityPricesBackfillExecutor"/>.
    /// </summary>
    /// <param name="scopeFactory">Scope factory used to create a scoped service provider for DB and provider services.</param>
    /// <param name="logger">Logger used for diagnostics and error reporting.</param>
    /// <param name="localizer">Localizer for user-facing progress messages.</param>
    public SecurityPricesBackfillExecutor(IServiceScopeFactory scopeFactory, ILogger<SecurityPricesBackfillExecutor> logger, IStringLocalizer<Pages> localizer)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Executes the backfill task for the current background task context. The payload may specify
    /// an optional security id and from/to date range. Progress is reported to the provided context.
    /// </summary>
    /// <param name="context">Context object that provides task payload, user id and progress reporting.</param>
    /// <param name="ct">Cancellation token to observe for cooperative cancellation.</param>
    /// <returns>A task that completes when the backfill run has finished or was cancelled.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no shared AlphaVantage key is configured.</exception>
    public async Task ExecuteAsync(BackgroundTaskContext context, CancellationToken ct)
    {
        // Parse payload
        Guid? onlySecurityId = null;
        DateTime? fromUtc = null;
        DateTime? toUtc = null;
        try
        {
            if (context.Payload is string raw && !string.IsNullOrWhiteSpace(raw))
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("SecurityId", out var sEl))
                {
                    if (Guid.TryParse(sEl.GetString(), out var sid)) { onlySecurityId = sid; }
                }
                if (doc.RootElement.TryGetProperty("FromDateUtc", out var fEl) && fEl.ValueKind == JsonValueKind.String)
                {
                    if (DateTime.TryParse(fEl.GetString(), out var fdt)) { fromUtc = fdt; }
                }
                if (doc.RootElement.TryGetProperty("ToDateUtc", out var tEl) && tEl.ValueKind == JsonValueKind.String)
                {
                    if (DateTime.TryParse(tEl.GetString(), out var tdt)) { toUtc = tdt; }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid backfill payload");
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var priceProvider = scope.ServiceProvider.GetRequiredService<IPriceProvider>();
        var keyResolver = scope.ServiceProvider.GetRequiredService<IAlphaVantageKeyResolver>();

        // Ensure shared key (worker context)
        var shared = await keyResolver.GetSharedAsync(ct);
        if (string.IsNullOrWhiteSpace(shared))
        {
            context.ReportProgress(0, 1, _localizer["NoSharedKey"], 0, 1);
            throw new InvalidOperationException("No shared AlphaVantage key configured");
        }

        var today = DateTime.UtcNow.Date;
        var endInclusiveBase = (toUtc?.Date ?? today.AddDays(-1));
        while (endInclusiveBase.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            endInclusiveBase = endInclusiveBase.AddDays(-1);
        }

        var defaultFromInclusive = endInclusiveBase.AddYears(-2);

        // Select securities (owned by user) and eligible
        var q = db.Securities.AsNoTracking()
            .Where(s => s.OwnerUserId == context.UserId && s.IsActive && s.AlphaVantageCode != null && !s.HasPriceError);
        if (onlySecurityId.HasValue) { q = q.Where(s => s.Id == onlySecurityId.Value); }

        var list = await q
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.OwnerUserId, s.Name, s.Identifier, s.AlphaVantageCode })
            .ToListAsync(ct);

        var total = list.Count;
        if (total == 0)
        {
            context.ReportProgress(1, 1, _localizer["NoSecurities"], 0, 0);
            return;
        }

        int processed = 0;
        foreach (var s in list)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Latest stored closing date for this security
                var lastStored = await db.SecurityPrices.AsNoTracking()
                    .Where(p => p.SecurityId == s.Id)
                    .OrderByDescending(p => p.Date)
                    .Select(p => p.Date)
                    .FirstOrDefaultAsync(ct);

                // Compute desired window [fromInclusive..toInclusive]
                DateTime fromInclusive;
                DateTime toInclusive;

                if (lastStored == default)
                {
                    // No data yet ? fetch from payload (or default) up to endInclusiveBase
                    fromInclusive = (fromUtc?.Date ?? defaultFromInclusive);
                    toInclusive = endInclusiveBase;
                }
                else if (fromUtc.HasValue && fromUtc.Value.Date <= lastStored.Date)
                {
                    // Backfill older history before lastStored
                    fromInclusive = fromUtc.Value.Date;
                    toInclusive = (endInclusiveBase <= lastStored.Date) ? endInclusiveBase : lastStored.Date.AddDays(-1);
                }
                else if (fromUtc.HasValue && fromUtc.Value.Date > lastStored.Date)
                {
                    // Forward fill starting after lastStored
                    fromInclusive = fromUtc.Value.Date;
                    toInclusive = endInclusiveBase;
                }
                else
                {
                    // No explicit from ? continue after lastStored
                    fromInclusive = lastStored.Date.AddDays(1);
                    toInclusive = endInclusiveBase;
                }

                if (toInclusive < fromInclusive)
                {
                    processed++;
                    context.ReportProgress(processed, total, s.Name, 0, 0);
                    continue;
                }

                // Map to provider semantics (startExclusive, endInclusive)
                var startExclusive = fromInclusive.AddDays(-1);
                var endInclusive = toInclusive;

                var data = await priceProvider.GetDailyPricesAsync(s.AlphaVantageCode!, startExclusive, endInclusive, ct);
                int inserts = 0;
                foreach (var (date, close) in data)
                {
                    if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) { continue; }
                    db.SecurityPrices.Add(new FinanceManager.Domain.Securities.SecurityPrice(s.Id, date, close));
                    inserts++;
                }
                if (inserts > 0) { await db.SaveChangesAsync(ct); }
                context.ReportProgress(++processed, total, s.Name, 0, 0);
            }
            catch (RequestLimitExceededException ex)
            {
                _logger.LogWarning(ex, "Rate limit reached during backfill for {Security}", s.Name);
                context.ReportProgress(processed, total, _localizer["RateLimited"], 0, 1);
                // Stop the whole backfill; user can rerun later
                throw;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid symbol for {Security}", s.Name);
                // mark error on entity and continue others
                var entity = await db.Securities.FirstOrDefaultAsync(x => x.Id == s.Id, ct);
                if (entity != null)
                {
                    entity.SetPriceError(ex.Message);
                    await db.SaveChangesAsync(ct);
                }
                context.ReportProgress(processed, total, s.Name + ": " + ex.Message, 0, 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backfill failed for {Security}", s.Name);
                context.ReportProgress(processed, total, ex.Message, 0, 1);
            }
        }

        context.ReportProgress(total, total, _localizer["Completed"], 0, 0);
    }
}

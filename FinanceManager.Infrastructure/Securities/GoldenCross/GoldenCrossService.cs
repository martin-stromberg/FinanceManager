using FinanceManager.Application.Notifications;
using FinanceManager.Application.Securities.GoldenCross;
using FinanceManager.Domain.Notifications;
using FinanceManager.Domain.Securities;
using FinanceManager.Shared.Dtos.Securities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceManager.Infrastructure.Securities.GoldenCross;

/// <summary>
/// Default implementation of <see cref="IGoldenCrossService"/> based on stored <see cref="SecurityPrice"/> rows.
/// </summary>
public sealed class GoldenCrossService : IGoldenCrossService
{
    /// <summary>Prefix of the notification trigger key; followed by the security id.</summary>
    public const string TriggerKeyPrefix = "security:golden-cross:";

    private readonly AppDbContext _db;
    private readonly INotificationWriter _notifications;
    private readonly IGoldenCrossLocalizer _localizer;
    private readonly GoldenCrossOptions _options;
    private readonly ILogger<GoldenCrossService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoldenCrossService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="notifications">Notification writer.</param>
    /// <param name="localizer">Localizer for notification texts.</param>
    /// <param name="options">Golden cross options.</param>
    /// <param name="logger">Logger.</param>
    public GoldenCrossService(
        AppDbContext db,
        INotificationWriter notifications,
        IGoldenCrossLocalizer localizer,
        IOptions<GoldenCrossOptions> options,
        ILogger<GoldenCrossService> logger)
    {
        _db = db;
        _notifications = notifications;
        _localizer = localizer;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GoldenCrossDto?> GetAsync(Guid securityId, Guid ownerUserId, CancellationToken ct)
    {
        var security = await _db.Securities.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == securityId && s.OwnerUserId == ownerUserId, ct);
        if (security == null)
        {
            return null;
        }

        var analysis = await AnalyzeAsync(securityId, ct);
        return new GoldenCrossDto(
            (GoldenCrossPhaseDto)(int)analysis.Phase,
            _options.ShortWindow,
            _options.LongWindow,
            analysis.ShortAverage,
            analysis.LongAverage,
            analysis.DistancePercent,
            analysis.CrossDate,
            analysis.AsOfDate,
            security.CurrencyCode,
            analysis.LastClose,
            analysis.DaysSinceCross,
            analysis.Windows.Select(w => new GoldenCrossEntryWindowDto(
                (GoldenCrossEntryScenarioDto)(int)w.Scenario,
                w.FromDays,
                w.ToDays,
                w.StartDate,
                w.EndDate,
                (GoldenCrossWindowStatusDto)(int)w.Status,
                w.LowClose,
                w.HighClose,
                w.SignalDate,
                w.Confirmed)).ToList(),
            analysis.Retest is { } r
                ? new GoldenCrossRetestDto(
                    (GoldenCrossRetestStatusDto)(int)r.Status,
                    r.Line.HasValue ? (GoldenCrossLineDto)(int)r.Line.Value : null,
                    r.StartDate,
                    r.LowDate,
                    r.LowClose,
                    r.EndDate)
                : null,
            analysis.Trend is { } t
                ? new GoldenCrossTrendDto(
                    (GoldenCrossTrendStructureDto)(int)t.Structure,
                    t.HigherHighs,
                    t.HigherLows,
                    t.SwingHighs,
                    t.SwingLows,
                    t.ChangeSinceCrossPercent,
                    t.ConfirmedDate)
                : null);
    }

    /// <inheritdoc />
    public async Task<bool> EvaluateAndNotifyAsync(Guid securityId, CancellationToken ct)
    {
        var security = await _db.Securities.FirstOrDefaultAsync(s => s.Id == securityId, ct);
        if (security == null)
        {
            return false;
        }

        var analysis = await AnalyzeAsync(securityId, ct);
        var phase = analysis.Phase;

        if (phase == GoldenCrossPhase.InsufficientData)
        {
            return false;
        }

        if (phase == GoldenCrossPhase.Far)
        {
            if (security.GoldenCrossNotifiedPhase != GoldenCrossPhase.Far)
            {
                security.SetGoldenCrossNotifiedPhase(GoldenCrossPhase.Far);
                await _db.SaveChangesAsync(ct);
            }
            return false;
        }

        if (phase <= security.GoldenCrossNotifiedPhase)
        {
            return false;
        }

        security.SetGoldenCrossNotifiedPhase(phase);
        await _db.SaveChangesAsync(ct);

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == security.OwnerUserId)
            .Select(u => new { u.GoldenCrossNotificationsEnabled, u.PreferredLanguage })
            .FirstOrDefaultAsync(ct);
        if (user == null || !user.GoldenCrossNotificationsEnabled)
        {
            return false;
        }

        var keySuffix = phase == GoldenCrossPhase.Crossed ? "Crossed" : "Approaching";
        var title = _localizer.Format($"GoldenCross_{keySuffix}_Title", user.PreferredLanguage, security.Name);
        var message = _localizer.Format(
            $"GoldenCross_{keySuffix}_Message",
            user.PreferredLanguage,
            security.Name,
            security.Identifier,
            _options.ShortWindow,
            _options.LongWindow,
            analysis.ShortAverage ?? 0m,
            analysis.LongAverage ?? 0m,
            security.CurrencyCode);

        await _notifications.CreateForUserAsync(
            security.OwnerUserId,
            title,
            message,
            NotificationType.EventDriven,
            NotificationTarget.HomePage,
            DateTime.UtcNow.Date,
            TriggerKeyPrefix + security.Id,
            ct);

        _logger.LogInformation("Golden cross notification ({Phase}) created for security {SecurityId}", phase, securityId);
        return true;
    }

    private async Task<GoldenCrossAnalysis> AnalyzeAsync(Guid securityId, CancellationToken ct)
    {
        var take = _options.LongWindow * 3;
        var rows = await _db.SecurityPrices.AsNoTracking()
            .Where(p => p.SecurityId == securityId)
            .OrderByDescending(p => p.Date)
            .Take(take)
            .Select(p => new { p.Date, p.Close })
            .ToListAsync(ct);

        rows.Reverse();
        var series = rows.Select(r => (r.Date, r.Close)).ToList();
        return GoldenCrossAnalyzer.Analyze(series, _options);
    }
}

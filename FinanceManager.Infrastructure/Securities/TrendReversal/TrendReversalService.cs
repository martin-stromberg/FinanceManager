using FinanceManager.Application.Notifications;
using FinanceManager.Application.Securities.TrendReversal;
using FinanceManager.Domain.Notifications;
using FinanceManager.Domain.Securities;
using FinanceManager.Shared.Dtos.Securities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceManager.Infrastructure.Securities.TrendReversal;

/// <summary>
/// Default implementation of <see cref="ITrendReversalService"/> based on stored <see cref="SecurityPrice"/> rows.
/// </summary>
public sealed class TrendReversalService : ITrendReversalService
{
    /// <summary>Prefix of the notification trigger key; followed by the security id.</summary>
    public const string TriggerKeyPrefix = "security:trend-reversal:";

    private readonly AppDbContext _db;
    private readonly INotificationWriter _notifications;
    private readonly ITrendReversalLocalizer _localizer;
    private readonly TrendReversalOptions _options;
    private readonly ILogger<TrendReversalService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrendReversalService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="notifications">Notification writer.</param>
    /// <param name="localizer">Localizer for notification texts.</param>
    /// <param name="options">Trend reversal options.</param>
    /// <param name="logger">Logger.</param>
    public TrendReversalService(
        AppDbContext db,
        INotificationWriter notifications,
        ITrendReversalLocalizer localizer,
        IOptions<TrendReversalOptions> options,
        ILogger<TrendReversalService> logger)
    {
        _db = db;
        _notifications = notifications;
        _localizer = localizer;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TrendReversalDto?> GetAsync(Guid securityId, Guid ownerUserId, CancellationToken ct)
    {
        var security = await _db.Securities.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == securityId && s.OwnerUserId == ownerUserId, ct);
        if (security == null)
        {
            return null;
        }

        var analysis = await AnalyzeAsync(securityId, ct);
        return new TrendReversalDto(
            (TrendStatusDto)(int)analysis.Status,
            _options.ShortWindow,
            _options.LongWindow,
            analysis.ShortAverage,
            analysis.LongAverage,
            analysis.DistancePercent,
            analysis.AsOfDate,
            security.CurrencyCode,
            analysis.LastClose,
            analysis.DeathCross is { } d
                ? new DeathCrossResultDto(d.CrossDate, d.DaysSinceCross, d.Active)
                : null,
            analysis.LineBreak is { } lb
                ? new LongTermLineBreakResultDto(
                    (LineBreakClassificationDto)(int)lb.Classification,
                    lb.BreakDate,
                    lb.ConsecutiveDaysBelow,
                    lb.PercentBelow)
                : null,
            analysis.MarketStructure is { } ms
                ? new MarketStructureResultDto(
                    (MarketStructureClassificationDto)(int)ms.Classification,
                    ms.LowerHighs,
                    ms.LowerLows,
                    ms.SwingHighs,
                    ms.SwingLows,
                    ms.ChangePercent)
                : null,
            analysis.Macd is { } m
                ? new MacdResultDto(m.Macd, m.Signal, m.Histogram, m.BearishCrossover, m.CrossoverDate)
                : null,
            analysis.Rsi is { } r
                ? new RsiResultDto(r.Rsi, r.BearishDivergence, r.DivergenceDate)
                : null,
            analysis.SignalList.Select(s => new ReversalSignalDto(
                (TrendReversalSignalTypeDto)(int)s.Type,
                (TrendReversalSignalStrengthDto)(int)s.Strength,
                s.Date)).ToList(),
            analysis.SignalScore);
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
        var status = analysis.Status;

        if (status == TrendStatus.InsufficientData)
        {
            return false;
        }

        if (status == TrendStatus.Intact)
        {
            if (security.TrendReversalNotifiedStatus != TrendStatus.Intact)
            {
                security.SetTrendReversalNotifiedStatus(TrendStatus.Intact);
                await _db.SaveChangesAsync(ct);
            }
            return false;
        }

        if (status <= security.TrendReversalNotifiedStatus)
        {
            return false;
        }

        security.SetTrendReversalNotifiedStatus(status);
        await _db.SaveChangesAsync(ct);

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == security.OwnerUserId)
            .Select(u => new { u.TrendReversalNotificationsEnabled, u.PreferredLanguage })
            .FirstOrDefaultAsync(ct);
        if (user == null || !user.TrendReversalNotificationsEnabled)
        {
            return false;
        }

        var keySuffix = status == TrendStatus.ReversalLikely ? "ReversalLikely" : "Weakened";
        var signalNames = string.Join(", ", analysis.SignalList
            .Select(s => _localizer.Format($"TrendReversal_Signal_{s.Type}", user.PreferredLanguage)));
        var title = _localizer.Format($"TrendReversal_{keySuffix}_Title", user.PreferredLanguage, security.Name);
        var message = _localizer.Format(
            $"TrendReversal_{keySuffix}_Message",
            user.PreferredLanguage,
            security.Name,
            security.Identifier,
            analysis.SignalList.Count,
            signalNames);

        await _notifications.CreateForUserAsync(
            security.OwnerUserId,
            title,
            message,
            NotificationType.EventDriven,
            NotificationTarget.HomePage,
            DateTime.UtcNow.Date,
            TriggerKeyPrefix + security.Id,
            ct);

        _logger.LogInformation("Trend reversal notification ({Status}) created for security {SecurityId}", status, securityId);
        return true;
    }

    private async Task<TrendReversalAnalysis> AnalyzeAsync(Guid securityId, CancellationToken ct)
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
        return TrendReversalAnalyzer.Analyze(series, _options);
    }
}

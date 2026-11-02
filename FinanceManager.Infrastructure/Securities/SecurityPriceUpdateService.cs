using FinanceManager.Application.Notifications;
using FinanceManager.Application.Securities;
using FinanceManager.Application.Securities.GoldenCross;
using FinanceManager.Application.Securities.TrendReversal;
using FinanceManager.Domain.Notifications;
using FinanceManager.Shared.Dtos.Securities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Securities;

/// <summary>
/// Implementation of <see cref="ISecurityPriceUpdateService"/>: the single pass entry point
/// for the security price maintenance process used by the scheduled worker, the manual
/// backfill task and file-based imports.
/// </summary>
public sealed class SecurityPriceUpdateService : ISecurityPriceUpdateService
{
    private readonly AppDbContext _db;
    private readonly IPriceProvider _priceProvider;
    private readonly ISecurityPriceService _priceService;
    private readonly INotificationWriter _notifier;
    private readonly IGoldenCrossService _goldenCross;
    private readonly ITrendReversalService _trendReversal;
    private readonly ILogger<SecurityPriceUpdateService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityPriceUpdateService"/> class.
    /// </summary>
    /// <param name="db">Application database context used to inspect the security error state.</param>
    /// <param name="priceProvider">Provider used to fetch daily prices.</param>
    /// <param name="priceService">Service used to persist prices and manage the price error state.</param>
    /// <param name="notifier">Writer used to notify the owner about persistent provider errors.</param>
    /// <param name="goldenCross">Service used for the golden cross signal evaluation.</param>
    /// <param name="trendReversal">Service used for the trend reversal signal evaluation.</param>
    /// <param name="logger">Logger used for diagnostic messages.</param>
    public SecurityPriceUpdateService(
        AppDbContext db,
        IPriceProvider priceProvider,
        ISecurityPriceService priceService,
        INotificationWriter notifier,
        IGoldenCrossService goldenCross,
        ITrendReversalService trendReversal,
        ILogger<SecurityPriceUpdateService> logger)
    {
        _db = db;
        _priceProvider = priceProvider;
        _priceService = priceService;
        _notifier = notifier;
        _goldenCross = goldenCross;
        _trendReversal = trendReversal;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SecurityPriceRefreshResult> RefreshFromProviderAsync(SecurityPriceRefreshRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<(DateTime date, decimal close)> prices;
        try
        {
            prices = await _priceProvider.GetDailyPricesAsync(request.AlphaVantageCode, request.StartExclusive, request.EndInclusive, ct);
        }
        catch (InvalidOperationException ex)
        {
            // Domain error (e.g. invalid symbol): mark security as errored and notify the owner.
            _logger.LogWarning(ex, "Price fetch error for security {SecurityId} ({Code}).", request.SecurityId, request.AlphaVantageCode);
            await MarkProviderErrorAsync(request, ex.Message, ct);
            return new SecurityPriceRefreshResult(SecurityPriceRefreshStatus.ProviderError, ErrorMessage: ex.Message);
        }

        await _priceService.ClearPriceErrorAsync(request.OwnerUserId, request.SecurityId, ct);

        if (prices.Count == 0)
        {
            return new SecurityPriceRefreshResult(SecurityPriceRefreshStatus.NoData);
        }

        var items = prices
            .Where(p => p.date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .Select(p => new SecurityPriceImportItem(p.date.Date, p.close, 0))
            .ToList();

        var storeResult = await StoreAndEvaluateAsync(request.OwnerUserId, request.SecurityId, items, ct);
        return new SecurityPriceRefreshResult(SecurityPriceRefreshStatus.Stored, storeResult);
    }

    /// <inheritdoc />
    public async Task<SecurityPriceImportResultDto> StoreAndEvaluateAsync(Guid ownerUserId, Guid securityId, IReadOnlyList<SecurityPriceImportItem> items, CancellationToken ct)
    {
        var result = await _priceService.UpsertDailyPricesAsync(ownerUserId, securityId, items, ct);

        try
        {
            await _goldenCross.EvaluateAndNotifyAsync(securityId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Golden cross evaluation failed for security {SecurityId}.", securityId);
        }

        try
        {
            await _trendReversal.EvaluateAndNotifyAsync(securityId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Trend reversal evaluation failed for security {SecurityId}.", securityId);
        }

        return result;
    }

    private async Task MarkProviderErrorAsync(SecurityPriceRefreshRequest request, string message, CancellationToken ct)
    {
        var entity = await _db.Securities.FirstOrDefaultAsync(s => s.Id == request.SecurityId && s.OwnerUserId == request.OwnerUserId, ct);
        if (entity is null)
        {
            return;
        }

        var wasAlreadyRaised = entity.HasPriceError && string.Equals(entity.PriceErrorMessage, message, StringComparison.Ordinal);
        await _priceService.SetPriceErrorAsync(request.OwnerUserId, request.SecurityId, message, ct);
        if (wasAlreadyRaised)
        {
            return;
        }

        var title = "Kursabruf fehlgeschlagen";
        var msg = $"Für '{request.SecurityName}' ({request.Identifier}) ist beim Kursabruf ein Fehler aufgetreten: {message}\nAndere Abrufe laufen weiter. Das Wertpapier bleibt als fehlerhaft markiert, bis ein Abruf erfolgreich ist.";
        var trigger = $"security:error:{request.SecurityId}";
        await _notifier.CreateForUserAsync(request.OwnerUserId, title, msg, NotificationType.SystemAlert, NotificationTarget.HomePage, DateTime.UtcNow.Date, trigger, ct);
    }
}

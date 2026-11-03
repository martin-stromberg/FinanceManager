using FinanceManager.Application.Notifications;
using FinanceManager.Application.Securities;
using FinanceManager.Domain.Notifications;
using FinanceManager.Shared.Dtos.Securities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Securities;

/// <summary>
/// Implementation of <see cref="ISecurityPriceUpdateService"/>: the single pass entry point
/// for the provider-based security price maintenance process used by the scheduled worker
/// and the manual backfill task.
/// </summary>
public sealed class SecurityPriceUpdateService : ISecurityPriceUpdateService
{
    private readonly AppDbContext _db;
    private readonly IPriceProvider _priceProvider;
    private readonly ISecurityPriceService _priceService;
    private readonly INotificationWriter _notifier;
    private readonly ISecurityPriceStoreAndEvaluateService _storeAndEvaluate;
    private readonly ILogger<SecurityPriceUpdateService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityPriceUpdateService"/> class.
    /// </summary>
    /// <param name="db">Application database context used to inspect the security error state.</param>
    /// <param name="priceProvider">Provider used to fetch daily prices.</param>
    /// <param name="priceService">Service used to manage the price error state.</param>
    /// <param name="notifier">Writer used to notify the owner about persistent provider errors.</param>
    /// <param name="storeAndEvaluate">Service used to persist fetched prices and run the signal evaluations.</param>
    /// <param name="logger">Logger used for diagnostic messages.</param>
    public SecurityPriceUpdateService(
        AppDbContext db,
        IPriceProvider priceProvider,
        ISecurityPriceService priceService,
        INotificationWriter notifier,
        ISecurityPriceStoreAndEvaluateService storeAndEvaluate,
        ILogger<SecurityPriceUpdateService> logger)
    {
        _db = db;
        _priceProvider = priceProvider;
        _priceService = priceService;
        _notifier = notifier;
        _storeAndEvaluate = storeAndEvaluate;
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

        var storeResult = await _storeAndEvaluate.StoreAndEvaluateAsync(request.OwnerUserId, request.SecurityId, items, ct);
        return new SecurityPriceRefreshResult(SecurityPriceRefreshStatus.Stored, storeResult);
    }

    private async Task MarkProviderErrorAsync(SecurityPriceRefreshRequest request, string message, CancellationToken ct)
    {
        var entity = await _db.Securities.FirstOrDefaultAsync(s => s.Id == request.SecurityId && s.OwnerUserId == request.OwnerUserId, ct);
        if (entity is null)
        {
            return;
        }

        // Security.SetPriceError normalizes null/whitespace to "Unknown error"; compare the normalized value.
        var normalized = string.IsNullOrWhiteSpace(message) ? "Unknown error" : message;
        var wasAlreadyRaised = entity.HasPriceError && string.Equals(entity.PriceErrorMessage, normalized, StringComparison.Ordinal);
        await _priceService.SetPriceErrorAsync(request.OwnerUserId, request.SecurityId, normalized, ct);
        if (wasAlreadyRaised)
        {
            return;
        }

        var title = "Kursabruf fehlgeschlagen";
        var msg = $"Für '{request.SecurityName}' ({request.Identifier}) ist beim Kursabruf ein Fehler aufgetreten: {normalized}\nAndere Abrufe laufen weiter. Das Wertpapier bleibt als fehlerhaft markiert, bis ein Abruf erfolgreich ist.";
        var trigger = $"security:error:{request.SecurityId}";
        await _notifier.CreateForUserAsync(request.OwnerUserId, title, msg, NotificationType.SystemAlert, NotificationTarget.HomePage, DateTime.UtcNow.Date, trigger, ct);
    }
}

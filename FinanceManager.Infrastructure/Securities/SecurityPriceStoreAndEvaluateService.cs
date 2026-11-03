using FinanceManager.Application.Securities;
using FinanceManager.Application.Securities.GoldenCross;
using FinanceManager.Application.Securities.TrendReversal;
using FinanceManager.Shared.Dtos.Securities;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Securities;

/// <summary>
/// Implementation of <see cref="ISecurityPriceStoreAndEvaluateService"/>: persists price
/// items via the upsert path (validation plus portfolio report cache invalidation) and
/// runs the signal evaluations. Evaluation failures are logged and do not fail the store.
/// </summary>
public sealed class SecurityPriceStoreAndEvaluateService : ISecurityPriceStoreAndEvaluateService
{
    private readonly ISecurityPriceService _priceService;
    private readonly IGoldenCrossService _goldenCross;
    private readonly ITrendReversalService _trendReversal;
    private readonly ILogger<SecurityPriceStoreAndEvaluateService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityPriceStoreAndEvaluateService"/> class.
    /// </summary>
    /// <param name="priceService">Service used to upsert the price items.</param>
    /// <param name="goldenCross">Service used for the golden cross signal evaluation.</param>
    /// <param name="trendReversal">Service used for the trend reversal signal evaluation.</param>
    /// <param name="logger">Logger used for diagnostic messages.</param>
    public SecurityPriceStoreAndEvaluateService(
        ISecurityPriceService priceService,
        IGoldenCrossService goldenCross,
        ITrendReversalService trendReversal,
        ILogger<SecurityPriceStoreAndEvaluateService> logger)
    {
        _priceService = priceService;
        _goldenCross = goldenCross;
        _trendReversal = trendReversal;
        _logger = logger;
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
}

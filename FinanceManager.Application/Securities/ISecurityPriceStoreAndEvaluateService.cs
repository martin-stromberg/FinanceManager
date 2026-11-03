using FinanceManager.Shared.Dtos.Securities;

namespace FinanceManager.Application.Securities;

/// <summary>
/// Store-and-evaluate part of the security price maintenance pass: persists already
/// provided price items and runs the signal evaluations (golden cross, trend reversal).
/// Used for sources that do not fetch from the price provider themselves (for example
/// file-based imports) and by <see cref="ISecurityPriceUpdateService"/> internally.
/// </summary>
public interface ISecurityPriceStoreAndEvaluateService
{
    /// <summary>
    /// Persists the given price items via upsert and runs the signal evaluations.
    /// </summary>
    /// <param name="ownerUserId">Owner of the security.</param>
    /// <param name="securityId">Target security identifier.</param>
    /// <param name="items">Price items to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Counters for inserted, updated and unchanged records.</returns>
    Task<SecurityPriceImportResultDto> StoreAndEvaluateAsync(Guid ownerUserId, Guid securityId, IReadOnlyList<SecurityPriceImportItem> items, CancellationToken ct);
}

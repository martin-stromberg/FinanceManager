using FinanceManager.Domain.Budget;
using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Application.Budget;

/// <summary>
/// Calculates planned budget values deterministically from rules and overrides.
/// </summary>
public interface IBudgetPlanningService
{
    /// <summary>
    /// Calculates planned values for the specified purposes and period range.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="purposeIds">Optional filter to a subset of purposes.</param>
    /// <param name="from">Start period (inclusive).</param>
    /// <param name="to">End period (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Planned values for each purpose and month in the requested range.</returns>
    Task<BudgetPlannedValuesResult> CalculatePlannedValuesAsync(
        Guid ownerUserId,
        IReadOnlyCollection<Guid>? purposeIds,
        BudgetPeriodKey from,
        BudgetPeriodKey to,
        CancellationToken ct);
}

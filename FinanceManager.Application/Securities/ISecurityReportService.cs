using FinanceManager.Shared.Dtos.Common;

namespace FinanceManager.Application.Securities;

/// <summary>
/// Read-only reporting operations for securities.
/// </summary>
public interface ISecurityReportService
{
    /// <summary>
    /// Returns quarterly dividend aggregates for the past year across all owned securities.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of quarterly aggregate points ordered by period start.</returns>
    Task<IReadOnlyList<AggregatePointDto>> GetDividendAggregatesAsync(Guid ownerUserId, CancellationToken ct);
}

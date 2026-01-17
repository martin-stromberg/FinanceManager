using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Application.Budget;

/// <summary>
/// Service for generating budget reports.
/// </summary>
public interface IBudgetReportService
{
    /// <summary>
    /// Generates a budget report for the current user.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="request">Report request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<BudgetReportDto> GetAsync(Guid ownerUserId, BudgetReportRequest request, CancellationToken ct);
}

using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Application.Budget;

/// <summary>
/// Service for generating budget reports.
/// </summary>
public interface IBudgetReportService
{
    /// <summary>
    /// Returns raw budget report data for the given date range.
    /// </summary>
    /// <param name="ownerUserId">The owner user id.</param>
    /// <param name="from">Inclusive range start.</param>
    /// <param name="to">Inclusive range end.</param>
    /// <param name="dateBasis">Date basis used when calculating actual values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A raw data DTO containing categories, purposes and contributing postings.</returns>
    Task<BudgetReportRawDataDto> GetRawDataAsync(
        Guid ownerUserId,
        DateOnly from,
        DateOnly to,
        BudgetReportDateBasis dateBasis,
        CancellationToken ct,
        bool ignoreCache = false);

    /// <summary>
    /// Asynchronously retrieves the monthly budget KPI data for the specified user and month.
    /// </summary>
    /// <param name="userId">The unique identifier of the user for whom to retrieve KPI data.</param>
    /// <param name="date">The month and year for which to retrieve KPI data. If null, the current month is used.</param>
    /// <param name="dateBasis">The date basis to use when calculating the KPI values.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="MonthlyBudgetKpiDto"/>
    /// with the KPI data for the specified user and month.</returns>
    Task<MonthlyBudgetKpiDto> GetMonthlyKpiAsync(Guid userId, DateOnly? date, BudgetReportDateBasis dateBasis, CancellationToken ct);
}

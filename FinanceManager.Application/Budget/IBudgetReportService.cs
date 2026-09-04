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
    /// <param name="ignoreCache">When true, bypasses any cached result and recomputes the data.</param>
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

    /// <summary>
    /// Builds the aggregated budget report (period table and category/purpose detail table) for the given
    /// user and range, using <c>Budgetbericht.GetCumulativeResult()</c> and <c>Budgetbericht.GetCurrentResult()</c>
    /// as the single source of truth for the aggregation and deviation calculation.
    /// </summary>
    /// <param name="ownerUserId">The owner user id.</param>
    /// <param name="asOfDate">Any date within the last (most recent) month of the report period.</param>
    /// <param name="months">Number of months to include in the report period, counting back from <paramref name="asOfDate"/>'s month.</param>
    /// <param name="interval">Interval to echo back on the resulting <see cref="BudgetReportDto"/>. The period table itself is always built at monthly granularity.</param>
    /// <param name="categoryValueScope">Whether the category/purpose table should reflect the whole report range or just its last month.</param>
    /// <param name="dateBasis">Date basis used when calculating actual values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The budget report DTO containing the period table and the category/purpose detail table.</returns>
    Task<BudgetReportDto> GetReportAsync(
        Guid ownerUserId,
        DateOnly asOfDate,
        int months,
        BudgetReportInterval interval,
        BudgetReportValueScope categoryValueScope,
        BudgetReportDateBasis dateBasis,
        CancellationToken ct);
}

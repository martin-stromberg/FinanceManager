namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Determines which date field is used for postings aggregation.
/// </summary>
public enum BudgetReportDateBasis
{
    /// <summary>
    /// Use the booking date.
    /// </summary>
    BookingDate = 0,

    /// <summary>
    /// Use the valuta/value date.
    /// </summary>
    ValutaDate = 1
}

/// <summary>
/// Request for generating a budget report.
/// </summary>
public sealed record BudgetReportRequest(
    DateOnly AsOfDate,
    int Months,
    BudgetReportInterval Interval,
    bool ShowTitle,
    bool ShowLineChart,
    bool ShowMonthlyTable,
    bool ShowDetailsTable,
    BudgetReportValueScope CategoryValueScope,
    bool IncludePurposeRows,
    BudgetReportDateBasis DateBasis);

/// <summary>
/// Determines which time scope is used for the category table values.
/// </summary>
public enum BudgetReportValueScope
{
    /// <summary>
    /// Use values for the whole selected report range.
    /// </summary>
    TotalRange = 0,

    /// <summary>
    /// Use values for the last calculated interval bucket.
    /// </summary>
    LastInterval = 1
}

/// <summary>
/// Interval of the aggregated budget report.
/// </summary>
public enum BudgetReportInterval
{
    /// <summary>
    /// Monthly periods.
    /// </summary>
    Month = 0,

    /// <summary>
    /// Quarterly periods.
    /// </summary>
    Quarter = 1,

    /// <summary>
    /// Yearly periods.
    /// </summary>
    Year = 2
}

/// <summary>
/// Budget report response payload.
/// </summary>
public sealed record BudgetReportDto(
    DateOnly RangeFrom,
    DateOnly RangeTo,
    BudgetReportInterval Interval,
    IReadOnlyList<BudgetReportPeriodDto> Periods,
    IReadOnlyList<BudgetReportCategoryDto> Categories);

/// <summary>
/// One period row of the budget report.
/// </summary>
public sealed record BudgetReportPeriodDto(
    DateOnly From,
    DateOnly To,
    decimal Budget,
    decimal Actual,
    decimal Delta,
    decimal DeltaPct);

/// <summary>
/// Identifies the semantic kind of a category row in the budget report.
/// </summary>
public enum BudgetReportCategoryRowKind
{
    /// <summary>
    /// Normal category row.
    /// </summary>
    Data = 0,

    /// <summary>
    /// Aggregated sum of all budget categories.
    /// </summary>
    Sum = 1,

    /// <summary>
    /// Postings that are not covered by any budget purpose.
    /// </summary>
    Unbudgeted = 2,

    /// <summary>
    /// Cost-neutral unbudgeted postings of the self contact (typically mirrored group postings).
    /// </summary>
    UnbudgetedSelfCostNeutral = 3,

    /// <summary>
    /// Final result (sum + unbudgeted).
    /// </summary>
    Result = 4
}

/// <summary>
/// Category section of the report.
/// </summary>
public sealed record BudgetReportCategoryDto(
    Guid Id,
    string Name,
    BudgetReportCategoryRowKind Kind,
    decimal Budget,
    decimal Actual,
    decimal Delta,
    decimal DeltaPct,
    IReadOnlyList<BudgetReportPurposeDto> Purposes);

/// <summary>
/// Purpose row inside a category.
/// </summary>
public sealed record BudgetReportPurposeDto(
    Guid Id,
    string Name,
    decimal Budget,
    decimal Actual,
    decimal Delta,
    decimal DeltaPct,
    BudgetSourceType SourceType,
    Guid SourceId);

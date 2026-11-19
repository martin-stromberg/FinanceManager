namespace FinanceManager.Shared.Dtos.Reports;

/// <summary>
/// Visualization type for a configurable aggregate report.
/// </summary>
public enum ReportChartType
{
    /// <summary>Historical sums over the selected periods.</summary>
    HistoricalSum = 0,
    /// <summary>Current-period allocation by entity.</summary>
    Allocation = 1,
    /// <summary>Current-period proportional distribution by entity.</summary>
    Pie = 2
}

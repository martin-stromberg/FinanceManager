namespace FinanceManager.Application.Securities.ReturnAnalysis;

/// <summary>Time range selection for the performance chart (FR-2.4).</summary>
public enum ChartTimeRange
{
    /// <summary>One month time range.</summary>
    OneMonth,

    /// <summary>Three months time range.</summary>
    ThreeMonths,

    /// <summary>Six months time range.</summary>
    SixMonths,

    /// <summary>One year time range.</summary>
    OneYear,

    /// <summary>Three years time range.</summary>
    ThreeYears,

    /// <summary>Entire available history.</summary>
    All
}

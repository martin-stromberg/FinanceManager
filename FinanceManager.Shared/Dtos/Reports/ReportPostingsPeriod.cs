namespace FinanceManager.Shared.Dtos.Reports;

/// <summary>
/// Selects which value column of a report cell the postings drill-down refers to.
/// </summary>
public enum ReportPostingsPeriod
{
    /// <summary>The current period amount (<c>Amount</c> column).</summary>
    Current = 0,

    /// <summary>The previous comparison period (<c>PreviousAmount</c> column).</summary>
    Previous = 1,

    /// <summary>The same period one year ago (<c>YearAgoAmount</c> column).</summary>
    YearAgo = 2
}

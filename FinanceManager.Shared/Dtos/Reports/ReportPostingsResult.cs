namespace FinanceManager.Shared.Dtos.Reports;

/// <summary>
/// Result of a report postings drill-down query.
/// </summary>
/// <param name="Postings">The postings composing the queried cell (sorted by effective date, descending).</param>
/// <param name="TotalCount">Total number of matching postings before truncation.</param>
/// <param name="TotalAmount">Exact sum of the amounts of all matching postings (independent of truncation).</param>
/// <param name="Truncated">True when <paramref name="Postings"/> was cut at the server side limit.</param>
/// <returns>The result.</returns>
public sealed record ReportPostingsResult(
    IReadOnlyList<ReportPostingDto> Postings,
    int TotalCount,
    decimal TotalAmount,
    bool Truncated
);

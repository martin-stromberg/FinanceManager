namespace FinanceManager.Shared.Dtos.Reports;

/// <summary>
/// Request payload that selects the individual postings behind a single report cell.
/// Carries the full aggregation context so the server can reproduce the cell's date
/// window and posting set.
/// </summary>
/// <param name="GroupKey">Group key of the report row (<c>Account:{id}</c>, <c>Contact:{id}</c>,
/// <c>SavingsPlan:{id}</c>, <c>Security:{id}</c>, <c>Security:{id}:{subType}</c>,
/// <c>Category:{kind}:{id|_none}</c>, <c>Type:{kind}</c>) or <c>null</c> for the total row.</param>
/// <param name="PeriodStart">Period anchor of the displayed row (its <see cref="ReportAggregatePointDto.PeriodStart"/>).</param>
/// <param name="Period">Value column the drill-down refers to.</param>
/// <param name="PostingKind">Primary posting kind.</param>
/// <param name="Interval">Report interval.</param>
/// <param name="IncludeCategory">Whether category grouping is active.</param>
/// <param name="CompareProjection">Whether dividend projection comparison is active.</param>
/// <param name="UseValutaDate">Whether the valuta date determines the effective posting date.</param>
/// <param name="PostingKinds">Optional multi-kind selection (falls back to <paramref name="PostingKind"/>).</param>
/// <param name="AnalysisDate">Optional analysis month anchor.</param>
/// <param name="Filters">Optional filters identical to the aggregate query.</param>
/// <param name="SavingsPlanPostingSource">Posting source used for emitted savings plans.</param>
/// <param name="Take">Period take of the aggregation context (drives the load window of netted dividend cells).</param>
/// <param name="CompareYear">Whether the year-ago comparison is active (widens the netted dividend load window).</param>
/// <returns>The result.</returns>
public sealed record ReportPostingsQueryRequest(
    string? GroupKey,
    DateTime PeriodStart,
    ReportPostingsPeriod Period,
    PostingKind PostingKind,
    ReportInterval Interval,
    bool IncludeCategory = false,
    bool CompareProjection = false,
    bool UseValutaDate = false,
    IReadOnlyCollection<PostingKind>? PostingKinds = null,
    DateTime? AnalysisDate = null,
    ReportAggregatesFiltersRequest? Filters = null,
    SavingsPlanPostingSource SavingsPlanPostingSource = SavingsPlanPostingSource.PlanPostings,
    int Take = 0,
    bool CompareYear = false
);

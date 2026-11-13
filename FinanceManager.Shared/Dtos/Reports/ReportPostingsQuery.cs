namespace FinanceManager.Shared.Dtos.Reports;

/// <summary>
/// Internal query describing which postings a single report cell is composed of.
/// Mirrors <see cref="ReportAggregationQuery"/> for the aggregation context and adds
/// the cell coordinates (group key, row period anchor and value column).
/// </summary>
/// <param name="OwnerUserId">Owner of the data (set server side, never by the client).</param>
/// <param name="GroupKey">Group key of the report row or <c>null</c> for the total row.</param>
/// <param name="PeriodStart">Period anchor of the displayed row.</param>
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
public sealed record ReportPostingsQuery(
    Guid OwnerUserId,
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
    ReportAggregationFilters? Filters = null,
    SavingsPlanPostingSource SavingsPlanPostingSource = SavingsPlanPostingSource.PlanPostings,
    int Take = 0,
    bool CompareYear = false
);

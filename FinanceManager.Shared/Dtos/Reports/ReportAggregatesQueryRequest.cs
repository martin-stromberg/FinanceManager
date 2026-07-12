using System.Text.Json.Serialization;

namespace FinanceManager.Shared.Dtos.Reports;

/// <summary>
/// Request payload to query aggregate reports for a given context.
/// </summary>
[method: JsonConstructor]
public sealed record ReportAggregatesQueryRequest(
    PostingKind PostingKind,
    ReportInterval Interval,
    int Take = 24,
    bool IncludeCategory = false,
    bool ComparePrevious = false,
    bool CompareYear = false,
    bool CompareProjection = false,
    bool UseValutaDate = false,
    IReadOnlyCollection<PostingKind>? PostingKinds = null,
    DateTime? AnalysisDate = null,
    ReportAggregatesFiltersRequest? Filters = null
)
{
    /// <summary>
    /// Compatibility constructor for callers that do not provide projection settings.
    /// </summary>
    public ReportAggregatesQueryRequest(
        PostingKind postingKind,
        ReportInterval interval,
        int take,
        bool includeCategory,
        bool comparePrevious,
        bool compareYear,
        bool useValutaDate,
        IReadOnlyCollection<PostingKind>? postingKinds,
        DateTime? analysisDate,
        ReportAggregatesFiltersRequest? filters)
        : this(postingKind, interval, take, includeCategory, comparePrevious, compareYear, false, useValutaDate, postingKinds, analysisDate, filters)
    {
    }
}

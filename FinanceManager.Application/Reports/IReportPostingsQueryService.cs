namespace FinanceManager.Application.Reports;

/// <summary>
/// Provides the drill-down query that lists the individual postings behind a single
/// aggregated report cell. The selection reproduces the semantics of
/// <see cref="IReportAggregationService"/> (ownership, filters, period windows and the
/// special paths for linked savings plan accounts and netted security dividends), so the
/// sum of the returned postings equals the displayed cell value.
/// </summary>
public interface IReportPostingsQueryService
{
    /// <summary>
    /// Queries the postings composing the report cell identified by the query.
    /// </summary>
    /// <param name="query">The cell coordinates plus the full aggregation context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The postings of the cell, truncated at a server side limit.</returns>
    /// <exception cref="ArgumentException">Thrown for unknown group key formats or comparison columns that are not available for the interval.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for unsupported enum values.</exception>
    Task<ReportPostingsResult> QueryAsync(ReportPostingsQuery query, CancellationToken ct);
}

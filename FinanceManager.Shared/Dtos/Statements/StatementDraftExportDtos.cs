namespace FinanceManager.Shared.Dtos.Statements;

/// <summary>
/// Request payload used to export the entries of a statement draft.
/// </summary>
/// <param name="Format">Export format, e.g., "csv" or "xlsx".</param>
/// <param name="From">Optional start date filter.</param>
/// <param name="To">Optional end date filter.</param>
/// <param name="Q">Optional search query string.</param>
/// <returns>The result.</returns>
public sealed record StatementDraftExportRequest(
    string? Format = "csv",
    DateTime? From = null,
    DateTime? To = null,
    string? Q = null
);

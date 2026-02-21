using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Application.Budget;

/// <summary>
/// Service to export all postings for a budget report over its full report range.
/// </summary>
public interface IBudgetReportExportService
{
    /// <summary>
    /// Creates an XLSX export for the given request.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="request">Export request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Content type, file name and readable stream (caller owns stream).</returns>
    Task<(string ContentType, string FileName, Stream Content)> GenerateXlsxAsync(Guid ownerUserId, BudgetReportExportRequest request, CancellationToken ct);
}

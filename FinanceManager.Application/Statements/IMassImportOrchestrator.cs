using FinanceManager.Shared.Dtos.Statements;

namespace FinanceManager.Application.Statements;

/// <summary>
/// Coordinates analysis and execution for mixed start-page mass imports.
/// </summary>
public interface IMassImportOrchestrator
{
    /// <summary>
    /// Analyzes uploaded files and optionally executes import operations.
    /// </summary>
    /// <param name="ownerUserId">Current user id.</param>
    /// <param name="request">Batch request containing files and optional decisions.</param>
    /// <param name="traceId">Current request trace id for audit logs.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Batch result with per-file status.</returns>
    Task<MassImportBatchResultDto> ProcessAsync(Guid ownerUserId, MassImportBatchRequestDto request, string traceId, CancellationToken ct);
}

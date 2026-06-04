using FinanceManager.Shared.Dtos.Statements;

namespace FinanceManager.Application.Statements;

/// <summary>
/// Evaluates budget impact for statement draft interactions and booking completion.
/// </summary>
public interface IBudgetImpactEvaluationService
{
    /// <summary>
    /// Evaluates budget impact for a single draft entry.
    /// </summary>
    Task<BudgetImpactEvaluationDto?> EvaluateEntryImpactAsync(Guid draftId, Guid entryId, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Evaluates budget impact summary for final booking scope.
    /// </summary>
    Task<BookingImpactSummaryDto?> EvaluateDraftImpactAsync(Guid draftId, Guid? entryId, Guid ownerUserId, CancellationToken ct);
}

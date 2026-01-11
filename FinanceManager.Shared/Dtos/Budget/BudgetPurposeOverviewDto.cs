namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Data transfer object for the budget purposes overview including rule count and computed budget for a period.
/// </summary>
/// <param name="Id">Purpose id.</param>
/// <param name="OwnerUserId">Owner user id.</param>
/// <param name="Name">Purpose name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="SourceType">Source type used to resolve actual values.</param>
/// <param name="SourceId">Identifier of the source entity.</param>
/// <param name="RuleCount">Number of configured rules for this purpose.</param>
/// <param name="BudgetSum">Computed budget sum for the provided period.</param>
/// <param name="SourceName">Resolved display name of the source entity.</param>
/// <param name="SourceSymbolAttachmentId">Optional symbol attachment id of the source entity.</param>
public sealed record BudgetPurposeOverviewDto(
    Guid Id,
    Guid OwnerUserId,
    string Name,
    string? Description,
    BudgetSourceType SourceType,
    Guid SourceId,
    int RuleCount,
    decimal BudgetSum,
    string? SourceName,
    Guid? SourceSymbolAttachmentId);

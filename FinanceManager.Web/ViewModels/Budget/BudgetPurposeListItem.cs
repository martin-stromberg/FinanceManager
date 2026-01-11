using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Web.ViewModels.Budget;

/// <summary>
/// Lightweight list item representing a budget purpose.
/// </summary>
/// <param name="Id">Purpose id.</param>
/// <param name="Name">Display name.</param>
/// <param name="SourceName">Resolved source display name.</param>
/// <param name="SourceSymbolAttachmentId">Optional symbol attachment id for the source entity.</param>
/// <param name="RuleCount">Number of budget rules configured for the purpose.</param>
/// <param name="BudgetSum">Computed budget sum for the selected range (or current month by default).</param>
public sealed record BudgetPurposeListItem(
    Guid Id,
    string Name,
    string SourceName,
    Guid? SourceSymbolAttachmentId,
    int RuleCount,
    decimal BudgetSum) : IListItemNavigation
{
    /// <inheritdoc />
    public string GetNavigateUrl() => $"/card/budget/purposes/{Id}";
}

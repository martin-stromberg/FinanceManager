using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Web.ViewModels.Budget;

/// <summary>
/// Lightweight list item representing a budget purpose.
/// </summary>
/// <param name="Id">Purpose id.</param>
/// <param name="Name">Display name.</param>
/// <param name="SourceType">Source type text.</param>
public sealed record BudgetPurposeListItem(Guid Id, string Name, string SourceType) : IListItemNavigation
{
    /// <inheritdoc />
    public string GetNavigateUrl() => $"/card/budget/purposes/{Id}";
}

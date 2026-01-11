using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Web.ViewModels.Budget;

/// <summary>
/// Lightweight list item for budget rule rows.
/// </summary>
/// <param name="Id">Rule id.</param>
/// <param name="Interval">Interval label.</param>
/// <param name="Amount">Amount formatted for display.</param>
/// <param name="Start">Start date formatted for display.</param>
/// <param name="End">End date formatted for display.</param>
public sealed record BudgetRuleListItem(
    Guid Id,
    string Interval,
    string Amount,
    string Start,
    string End) : IListItemNavigation
{
    /// <inheritdoc />
    public string GetNavigateUrl() => $"/card/budget/rules/{Id}";
}

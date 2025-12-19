using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Web.ViewModels.SavingsPlans;

public sealed record SavingsPlanListItem(Guid Id, string Name, string Type, string? CategoryName, Guid? SymbolId) : IListItemNavigation
{
    public string GetNavigateUrl() => $"/card/savings-plans/{Id}";
}

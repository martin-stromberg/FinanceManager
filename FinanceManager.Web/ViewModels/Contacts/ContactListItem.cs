using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Web.ViewModels.Contacts;

public sealed record ContactListItem(Guid Id, string Name, string Type, string? CategoryName, Guid? SymbolId) : IListItemNavigation
{
    public string GetNavigateUrl() => $"/card/contacts/{Id}";
}

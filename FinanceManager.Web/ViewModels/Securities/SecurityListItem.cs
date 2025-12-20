namespace FinanceManager.Web.ViewModels.Securities;

public sealed record SecurityListItem(Guid Id, string Name, string Identifier, string? AlphaVantageCode, Guid? CategoryId, string? CategoryName, bool IsActive, Guid? SymbolId) : IListItemNavigation
{
    public string GetNavigateUrl() => $"/card/securities/{Id}";
}

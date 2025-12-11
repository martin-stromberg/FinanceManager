namespace FinanceManager.Web.ViewModels.Accounts
{
    public sealed record AccountListItem(Guid Id, string Name, string Type, string? Iban, decimal CurrentBalance, Guid? SymbolId) : IListItemNavigation
    {
        public string GetNavigateUrl() => $"/card/accounts/{Id}";
    }
}

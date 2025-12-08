namespace FinanceManager.Web.ViewModels.Common
{
    public enum ListCellKind
    {
        Text,
        Symbol,
        Currency
    }

    public enum ListColumnAlign { Left, Center, Right }

    public sealed record ListColumn(string Key, string Title, string? Width = null, ListColumnAlign Align = ListColumnAlign.Left);

    // ListCell supports different kinds: Text (string), Symbol (attachment id), Currency (decimal)
    public sealed record ListCell(ListCellKind Kind, string? Text = null, System.Guid? SymbolId = null, decimal? Amount = null, string? IconUrl = null);

    public sealed record ListRecord(IReadOnlyList<ListCell> Cells, object? Item = null);

    // Card rendering types
    public enum CardFieldKind { Text, Symbol, Currency }

    public sealed record CardField(string LabelKey, CardFieldKind Kind, string? Text = null, System.Guid? SymbolId = null, decimal? Amount = null);

    public sealed record CardRecord(IReadOnlyList<CardField> Fields, object? Item = null);
}

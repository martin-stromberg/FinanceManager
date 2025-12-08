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

    public sealed class CardField
    {
        public string LabelKey { get; set; }
        public CardFieldKind Kind { get; set; }
        public string? Text { get; set; }
        public System.Guid? SymbolId { get; set; }
        public decimal? Amount { get; set; }
        public bool Editable { get; set; }
        public string? LookupType { get; set; }
        public string? LookupField { get; set; }
        // Optional free-form filter hint used by QueryLookup implementations (e.g. "Type=Bank")
        public string? LookupFilter { get; set; }
        public System.Guid? ValueId { get; set; }

        public CardField(string labelKey, CardFieldKind kind, string? text = null, System.Guid? symbolId = null, decimal? amount = null, bool editable = false, string? lookupType = null, string? lookupField = null, System.Guid? valueId = null, string? lookupFilter = null)
        {
            LabelKey = labelKey;
            Kind = kind;
            Text = text;
            SymbolId = symbolId;
            Amount = amount;
            Editable = editable;
            LookupType = lookupType;
            LookupField = lookupField;
            LookupFilter = lookupFilter;
            ValueId = valueId;
        }
    }

    public sealed record CardRecord(IReadOnlyList<CardField> Fields, object? Item = null);

    // Lookup item used for selection lists (key = primary key, name = display name)
    public sealed record LookupItem(System.Guid Key, string Name);
}

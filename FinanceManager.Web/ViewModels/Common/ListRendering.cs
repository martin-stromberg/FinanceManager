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
    // Added: Muted flag to allow rendering cells/rows in a muted (grayed-out) style when needed.
    public sealed record ListCell(ListCellKind Kind, string? Text = null, System.Guid? SymbolId = null, decimal? Amount = null, string? IconUrl = null, bool Muted = false);

    // ListRecord now optionally contains a Hint that can be rendered as a full-width row below the record.
    public sealed record ListRecord(IReadOnlyList<ListCell> Cells, object? Item = null, string? Hint = null);

    // Card rendering types
    public enum CardFieldKind { Text, Date, Boolean, Symbol, Currency }

    public sealed class CardField
    {
        public string LabelKey { get; set; }
        public CardFieldKind Kind { get; set; }
        public string? Text { get; set; }
        public System.Guid? SymbolId { get; set; }
        public decimal? Amount { get; set; }
        public bool? BoolValue { get; set; }
        public bool Editable { get; set; }
        public string? LookupType { get; set; }
        public string? LookupField { get; set; }
        // Optional free-form filter hint used by QueryLookup implementations (e.g. "Type=Bank")
        public string? LookupFilter { get; set; }
        // Optional hint text displayed under the field (spans full width of card table)
        public string? Hint { get; set; }
        public System.Guid? ValueId { get; set; }
        // When true, render an Add/New button next to lookup input so the user can create a new entity
        public bool AllowAdd { get; set; }
        // Optional suggestion used when creating a new record from this lookup (e.g. prefill name)
        public string? RecordCreationNameSuggestion { get; set; }

        public CardField(string labelKey, CardFieldKind kind, string? text = null, System.Guid? symbolId = null, decimal? amount = null, bool? boolValue = null, bool editable = false, string? lookupType = null, string? lookupField = null, System.Guid? valueId = null, string? lookupFilter = null, string? hint = null, bool allowAdd = false, string? recordCreationNameSuggestion = null)
        {
            LabelKey = labelKey;
            Kind = kind;
            Text = text;
            SymbolId = symbolId;
            Amount = amount;
            BoolValue = boolValue;
            Editable = editable;
            LookupType = lookupType;
            LookupField = lookupField;
            LookupFilter = lookupFilter;
            ValueId = valueId;
            Hint = hint;
            AllowAdd = allowAdd;
            RecordCreationNameSuggestion = recordCreationNameSuggestion;
        }
    }

    public sealed record CardRecord(IReadOnlyList<CardField> Fields, object? Item = null);
}

namespace FinanceManager.Web.ViewModels.Common
{
    public abstract class BaseListViewModel<TItem> : BaseViewModel, IListProvider
    {
        protected BaseListViewModel(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        public List<TItem> Items { get; } = new();
        IReadOnlyList<object> IListProvider.Items => Items.Cast<object>().ToList();
        public bool CanLoadMore { get; protected set; }

        public string Search { get; private set; } = string.Empty;
        public DateTime? RangeFrom { get; private set; }
        public DateTime? RangeTo { get; private set; }

        // New: columns metadata and rendered records for GenericListPage
        public IReadOnlyList<ListColumn> Columns { get; protected set; } = Array.Empty<ListColumn>();
        public IReadOnlyList<ListRecord> Records { get; protected set; } = Array.Empty<ListRecord>();
        public virtual bool AllowRangeFiltering { get; protected set; } = true;

        public virtual Task InitializeAsync() => LoadAsync();

        public async Task LoadAsync()
        {
            Loading = true; RaiseStateChanged();
            try
            {
                Items.Clear();
                CanLoadMore = false;
                await LoadPageAsync(resetPaging: true);
                BuildRecords();
            }
            finally { Loading = false; RaiseStateChanged(); }
        }

        public async Task LoadMoreAsync()
        {
            if (!CanLoadMore) return;
            Loading = true; RaiseStateChanged();
            try { await LoadPageAsync(resetPaging: false); BuildRecords(); }
            finally { Loading = false; RaiseStateChanged(); }
        }

        protected abstract Task LoadPageAsync(bool resetPaging);

        protected virtual void BuildRecords()
        {
            // Default: map Items.ToString into single text cell
            Columns = new[] { new ListColumn("__default", "Item") };
            Records = Items.Select(i => new ListRecord(new[] { new ListCell(ListCellKind.Text, Text: i?.ToString() ?? string.Empty) }, i)).ToList();
        }

        void IListProvider.SetSearch(string value) => SetSearch(value);
        void IListProvider.SetRange(DateTime? from, DateTime? to) => SetRange(from, to);

        public void SetSearch(string value)
        {
            Search = value ?? string.Empty;
        }

        public void ClearSearch() => SetSearch(string.Empty);

        public void SetRange(DateTime? from, DateTime? to)
        {
            RangeFrom = from; RangeTo = to;
        }

        public void ClearRange() => SetRange(null, null);

        public void ResetAndSearch()
        {
            Items.Clear();
            CanLoadMore = true;
        }
    }
}

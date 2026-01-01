using FinanceManager.Application;

namespace FinanceManager.Web.ViewModels.Common
{
    /// <summary>
    /// Non-generic base type for list view models. Allows other viewmodels (for example card viewmodels)
    /// to hold a reference to an embedded list without knowing the concrete item type parameter.
    /// </summary>
    public abstract class BaseListViewModel : BaseViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BaseListViewModel"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider used to resolve dependencies.</param>
        protected BaseListViewModel(IServiceProvider serviceProvider) : base(serviceProvider) { }
    }

    /// <summary>
    /// Generic base class for list view models providing common paging, filtering and record building functionality.
    /// Derived classes must implement page loading logic via <see cref="LoadPageAsync(bool)"/> and may override record building.
    /// </summary>
    /// <typeparam name="TItem">Type of the items contained in the list.</typeparam>
    public abstract class BaseListViewModel<TItem> : BaseListViewModel, IListProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BaseListViewModel{TItem}"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider used to resolve dependencies (API client, localizer, etc.).</param>
        protected BaseListViewModel(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        /// <summary>
        /// The list of items currently loaded by the view model. Derived classes append to this collection when pages are loaded.
        /// </summary>
        public List<TItem> Items { get; } = new();

        IReadOnlyList<object> IListProvider.Items => Items.Cast<object>().ToList();

        /// <summary>
        /// Indicates whether more pages are available to load.
        /// </summary>
        public bool CanLoadMore { get; protected set; }

        /// <summary>
        /// Current search string used to filter items. Use <see cref="SetSearch(string)"/> to update.
        /// </summary>
        public string Search { get; private set; } = string.Empty;

        /// <summary>
        /// Optional start date used for range filtering.
        /// </summary>
        public DateTime? RangeFrom { get; private set; }

        /// <summary>
        /// Optional end date used for range filtering.
        /// </summary>
        public DateTime? RangeTo { get; private set; }

        /// <summary>
        /// Column metadata used by the generic list renderer.
        /// </summary>
        public IReadOnlyList<ListColumn> Columns { get; protected set; } = Array.Empty<ListColumn>();

        /// <summary>
        /// Rendered records derived from <see cref="Items"/> and <see cref="Columns"/>.
        /// </summary>
        public IReadOnlyList<ListRecord> Records { get; protected set; } = Array.Empty<ListRecord>();

        /// <summary>
        /// Controls whether date range filtering is allowed for this list. Default is <c>true</c>.
        /// Derived classes may override to disable range filtering.
        /// </summary>
        public virtual bool AllowRangeFiltering { get; protected set; } = true;

        /// <summary>
        /// Controls whether search filtering is allowed for this list. Default is <c>true</c>.
        /// </summary>
        public virtual bool AllowSearchFiltering { get; protected set; } = true;

        /// <summary>
        /// Indicates whether the initial load has completed.
        /// Many pages and tests use this flag to detect initial load completion.
        /// </summary>
        public bool Loaded { get; protected set; }

        /// <summary>
        /// Performs initial initialization and triggers loading of the first page.
        /// The default implementation checks authentication and then calls <see cref="LoadAsync"/>
        /// </summary>
        /// <returns>A task that completes when initialization and the first load have finished.</returns>
        public virtual async Task InitializeAsync()
        {
            if (!CheckAuthentication())
            {
                return;
            }

            await LoadAsync();
        }

        /// <summary>
        /// Backwards-compatible initialization entry point that forwards to <see cref="InitializeAsync"/>
        /// </summary>
        /// <returns>A task that completes when initialization has finished.</returns>
        public virtual async Task InitializeAsyncWithAuth()
        {
            // kept for compatibility but forwards to InitializeAsync
            await InitializeAsync();
        }

        /// <summary>
        /// Loads the first page of items, clearing existing items and rebuilding records.
        /// Any exceptions thrown by <see cref="LoadPageAsync(bool)"/> propagate to the caller.
        /// </summary>
        /// <returns>A task that completes when loading has finished.</returns>
        public async Task LoadAsync()
        {
            Loading = true; Loaded = false; RaiseStateChanged();
            try
            {
                Items.Clear();
                CanLoadMore = false;
                await LoadPageAsync(resetPaging: true);
                BuildRecords();
            }
            finally { Loading = false; Loaded = true; RaiseStateChanged(); }
        }

        /// <summary>
        /// Loads the next page of items and appends them to <see cref="Items"/>. If no more pages are available this is a no-op.
        /// </summary>
        /// <returns>A task that completes when the page load has finished.</returns>
        public async Task LoadMoreAsync()
        {
            if (!CanLoadMore) return;
            Loading = true; RaiseStateChanged();
            try { await LoadPageAsync(resetPaging: false); BuildRecords(); }
            finally { Loading = false; RaiseStateChanged(); }
        }

        /// <summary>
        /// Loads a page of items from the underlying data source.
        /// Implementations should append results to <see cref="Items"/> and set <see cref="CanLoadMore"/> accordingly.
        /// </summary>
        /// <param name="resetPaging">When <c>true</c> the implementation should reset any paging offset and load from the first page; otherwise load the next page.</param>
        /// <returns>A task that completes when the page load has completed.</returns>
        protected abstract Task LoadPageAsync(bool resetPaging);

        /// <summary>
        /// Builds <see cref="Columns"/> and <see cref="Records"/> from the current <see cref="Items"/>.
        /// Derived classes should override to provide column definitions and record mapping. The default implementation
        /// maps the item's ToString() into a single text column.
        /// </summary>
        protected virtual void BuildRecords()
        {
            // Default: map Items.ToString into single text cell
            Columns = new[] { new ListColumn("__default", "Item") };
            Records = Items.Select(i => new ListRecord(new[] { new ListCell(ListCellKind.Text, Text: i?.ToString() ?? string.Empty) }, i)).ToList();
        }

        void IListProvider.SetSearch(string value) => SetSearch(value);
        void IListProvider.SetRange(DateTime? from, DateTime? to) => SetRange(from, to);

        /// <summary>
        /// Sets the search string used to filter results. This only updates the internal state; callers must trigger loading.
        /// </summary>
        /// <param name="value">Search string to apply; may be <c>null</c> to clear.</param>
        public void SetSearch(string value)
        {
            Search = value ?? string.Empty;
        }

        /// <summary>
        /// Clears the current search string.
        /// </summary>
        public void ClearSearch() => SetSearch(string.Empty);

        /// <summary>
        /// Sets the inclusive date range used for filtering items.
        /// </summary>
        /// <param name="from">Optional start date of the range.</param>
        /// <param name="to">Optional end date of the range.</param>
        public void SetRange(DateTime? from, DateTime? to)
        {
            RangeFrom = from; RangeTo = to;
        }

        /// <summary>
        /// Clears any applied date range filters.
        /// </summary>
        public void ClearRange() => SetRange(null, null);

        /// <summary>
        /// Resets internal items and marks the list as able to load more pages. Callers typically call this before performing a search.
        /// </summary>
        public void ResetAndSearch()
        {
            Items.Clear();
            CanLoadMore = true;
        }
    }
}

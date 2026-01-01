namespace FinanceManager.Web.ViewModels.Common
{
    /// <summary>
    /// Abstraction for list view models used by the UI. Provides access to the underlying items,
    /// rendered records/columns and common list operations such as paging, searching and range filtering.
    /// Implementations are used by generic list pages/components to interact with view model specific logic.
    /// </summary>
    public interface IListProvider
    {
        /// <summary>
        /// Underlying item collection exposed as read-only objects. Implementations typically project domain DTOs
        /// into a list of view model items.
        /// </summary>
        IReadOnlyList<object> Items { get; }

        /// <summary>
        /// Indicates whether more pages are available to load.
        /// </summary>
        bool CanLoadMore { get; }

        /// <summary>
        /// Indicates whether the view model is currently performing a background load operation.
        /// </summary>
        bool Loading { get; }

        /// <summary>
        /// Current search string used for filtering results.
        /// </summary>
        string Search { get; }

        /// <summary>
        /// Optional start date for range/date filtering.
        /// </summary>
        DateTime? RangeFrom { get; }

        /// <summary>
        /// Optional end date for range/date filtering.
        /// </summary>
        DateTime? RangeTo { get; }

        /// <summary>
        /// Column metadata used by the generic list renderer.
        /// </summary>
        IReadOnlyList<ListColumn> Columns { get; }

        /// <summary>
        /// Rendered records derived from <see cref="Items"/> used by the list renderer.
        /// </summary>
        IReadOnlyList<ListRecord> Records { get; }

        /// <summary>
        /// Whether the list supports range/date filtering. The UI can hide range controls when false.
        /// </summary>
        bool AllowRangeFiltering { get; }

        /// <summary>
        /// Whether the list supports text search filtering. The UI can hide the search control when false.
        /// </summary>
        bool AllowSearchFiltering { get; }

        /// <summary>
        /// Event raised when the view model's state changes and the UI should re-render.
        /// </summary>
        event EventHandler? StateChanged;

        /// <summary>
        /// Performs any initialization required by the provider and triggers an initial load if necessary.
        /// </summary>
        /// <returns>A task that completes when initialization has finished.</returns>
        Task InitializeAsync();

        /// <summary>
        /// Loads the first page of items and rebuilds <see cref="Records"/>. Implementations should set <see cref="Loading"/> appropriately.
        /// </summary>
        /// <returns>A task that completes when loading has finished.</returns>
        Task LoadAsync();

        /// <summary>
        /// Loads the next page of items when <see cref="CanLoadMore"/> is true and appends results to <see cref="Items"/>.
        /// </summary>
        /// <returns>A task that completes when the page load has finished.</returns>
        Task LoadMoreAsync();

        /// <summary>
        /// Clears the current search string. Callers should typically call <see cref="LoadAsync"/> afterwards to refresh results.
        /// </summary>
        void ClearSearch();

        /// <summary>
        /// Clears any applied date range filters.
        /// </summary>
        void ClearRange();

        /// <summary>
        /// Sets the search string used to filter results. This updates internal state; callers must trigger loading.
        /// </summary>
        /// <param name="value">Search string to apply; may be <c>null</c> to clear.</param>
        void SetSearch(string value);

        /// <summary>
        /// Sets the inclusive date range used for filtering items.
        /// </summary>
        /// <param name="from">Optional start date of the range.</param>
        /// <param name="to">Optional end date of the range.</param>
        void SetRange(DateTime? from, DateTime? to);

        /// <summary>
        /// Resets internal items and marks the list as able to load more pages. Typically used before performing a new search.
        /// </summary>
        void ResetAndSearch();

        /// <summary>
        /// Returns ribbon register definitions that should be displayed together with the list view.
        /// Implementations may return <c>null</c> when no ribbon is required.
        /// </summary>
        /// <param name="localizer">Localizer used to resolve localized UI labels for ribbon actions.</param>
        /// <returns>A list of <see cref="UiRibbonRegister"/> or <c>null</c> when no registers are provided.</returns>
        IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(Microsoft.Extensions.Localization.IStringLocalizer localizer);
    }
}

namespace FinanceManager.Web.ViewModels.Common
{
    public interface IListProvider
    {
        IReadOnlyList<object> Items { get; }
        bool CanLoadMore { get; }
        bool Loading { get; }
        string Search { get; }
        DateTime? RangeFrom { get; }
        DateTime? RangeTo { get; }

        IReadOnlyList<ListColumn> Columns { get; }
        IReadOnlyList<ListRecord> Records { get; }

        // Whether the list supports range/date filtering (UI can hide range controls if false)
        bool AllowRangeFiltering { get; }
        bool AllowSearchFiltering { get; }

        event EventHandler? StateChanged;

        Task InitializeAsync();
        Task LoadAsync();
        Task LoadMoreAsync();

        void ClearSearch();
        void ClearRange();
        void SetSearch(string value);
        void SetRange(DateTime? from, DateTime? to);
        void ResetAndSearch();

        IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(Microsoft.Extensions.Localization.IStringLocalizer localizer);
    }
}

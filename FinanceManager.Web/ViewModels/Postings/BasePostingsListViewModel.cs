using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Web.ViewModels.Postings
{
    public abstract class BasePostingsListViewModel : BaseListViewModel<PostingServiceDto>
    {
        protected BasePostingsListViewModel(IServiceProvider services) : base(services)
        {
            // default columns: Date | Text | Amount
            Columns = new[] {
                new ListColumn("date", "Date", Align: ListColumnAlign.Center, Width: "8rem"),
                new ListColumn("text", "Text"),
                new ListColumn("amount", "Amount", Align: ListColumnAlign.Right, Width: "10rem")
            };
            _take = 50;
        }

        protected int _take;
        protected int _skip;

        protected void ResetPaging() { _skip = 0; CanLoadMore = true; }

        protected override async Task LoadPageAsync(bool resetPaging)
        {
            if (resetPaging) { ResetPaging(); Items.Clear(); }

            var api = ServiceProvider.GetRequiredService<IApiClient>();
            var list = await QueryPageAsync(api, _skip, _take, Search, RangeFrom, RangeTo);
            if (resetPaging)
            {
                Items.Clear();
            }
            if (list != null && list.Count > 0)
            {
                Items.AddRange(list);
                _skip += list.Count;
                CanLoadMore = list.Count >= _take;
            }
            else
            {
                CanLoadMore = false;
            }
        }

        // Implemented by derived classes to query postings according to their filter
        protected abstract Task<IReadOnlyList<PostingServiceDto>?> QueryPageAsync(IApiClient api, int skip, int take, string search, DateTime? from, DateTime? to);

        protected override void BuildRecords()
        {
            Columns = Columns ?? Array.Empty<ListColumn>();
            Records = Items.Select(i => new ListRecord(new[] {
                new ListCell(ListCellKind.Text, Text: i.ValutaDate.ToString("d")),
                new ListCell(ListCellKind.Text, Text: i.Subject ?? string.Empty),
                new ListCell(ListCellKind.Currency, Amount: i.Amount)
            }, i)).ToList();
        }
    }
}

using FinanceManager.Shared;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Postings.Common
{
    public abstract class BasePostingsListViewModel : BaseListViewModel<PostingServiceDto>
    {
        protected BasePostingsListViewModel(IServiceProvider services) : base(services)
        {
            var dateLabel = Localizer?["List_Th_Postings_Date"].Value ?? "Date";
            var valutaLabel = Localizer?["List_Th_Postings_Valuta"].Value ?? "Valuta";
            var amountLabel = Localizer?["List_Th_Postings_Amount"].Value ?? "Amount";
            var kindLabel = Localizer?["List_Th_Postings_Kind"].Value ?? "Kind";
            var recipientLabel = Localizer?["List_Th_Postings_Recipient"].Value ?? "Recipient";
            var subjectLabel = Localizer?["List_Th_Postings_Subject"].Value ?? "Subject";
            var descriptionLabel = Localizer?["List_Th_Postings_Description"].Value ?? "Description";

            Columns = new[] {
                new ListColumn("date", dateLabel, Align: ListColumnAlign.Left, Width: "8rem"),
                new ListColumn("valuta", valutaLabel, Align: ListColumnAlign.Left, Width: "8rem"),
                new ListColumn("amount", amountLabel, Align: ListColumnAlign.Right, Width: "10rem"),
                new ListColumn("kind", kindLabel, Align: ListColumnAlign.Left, Width: "9rem"),
                new ListColumn("recipient", recipientLabel),
                new ListColumn("subject", subjectLabel, Width: "22%"),
                new ListColumn("description", descriptionLabel)
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
            Records = Items.Select(i =>
            {
                // create a navigation wrapper item that implements IListItemNavigation
                var navItem = new PostingListItem(i);
                return new ListRecord(new[] {
                    new ListCell(ListCellKind.Text, Text: i.BookingDate.ToString("d")),
                    new ListCell(ListCellKind.Text, Text: i.ValutaDate.ToString("d")),
                    new ListCell(ListCellKind.Currency, Amount: i.Amount),
                    new ListCell(ListCellKind.Text, Text: (i.Kind == PostingKind.Security && i.SecuritySubType.HasValue) ? $"Security-{i.SecuritySubType}" : i.Kind.ToString()),
                    new ListCell(ListCellKind.Text, Text: i.RecipientName ?? string.Empty),
                    new ListCell(ListCellKind.Text, Text: i.Subject ?? string.Empty),
                    new ListCell(ListCellKind.Text, Text: i.Description ?? string.Empty)
                }, navItem);
            }).ToList();
        }

        // navigation wrapper record for postings
        private sealed record PostingListItem(PostingServiceDto Posting) : IListItemNavigation
        {
            public string GetNavigateUrl()
            {
                // navigate to card page for posting
                var kind = "postings";
                return $"/card/{kind}/{Posting.Id}";
            }
        }

        public virtual string GetNavigateUrl()
        {
            return string.Empty;
        }

        // Build an export URL for postings lists. Derived classes can override to provide specific base path.
        protected string BuildExportUrl(string basePath, string format)
        {
            var q = new List<string>();
            if (!string.IsNullOrWhiteSpace(format)) q.Add($"format={Uri.EscapeDataString(format)}");
            if (!string.IsNullOrWhiteSpace(Search)) q.Add($"q={Uri.EscapeDataString(Search)}");
            if (RangeFrom.HasValue) q.Add($"from={RangeFrom.Value:yyyy-MM-dd}");
            if (RangeTo.HasValue) q.Add($"to={RangeTo.Value:yyyy-MM-dd}");
            return q.Count == 0 ? basePath : basePath + "?" + string.Join('&', q);
        }

        public virtual string GetExportUrl(string format) => string.Empty;

        // Default Ribbon for postings lists: Navigation (Back) and Export (CSV/XLSX)
        protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
        {
            var tabs = new List<UiRibbonTab>
            {
                new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, new List<UiRibbonAction>
                {
                    new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Large, false, null, "Back", new Func<Task>(()=>{ RaiseUiActionRequested("Back"); return Task.CompletedTask; })),
                }),
                new UiRibbonTab(localizer["Ribbon_Group_Export"].Value, new List<UiRibbonAction>
                {
                    new UiRibbonAction("ExportCsv", localizer["Ribbon_ExportCsv"].Value, "<svg><use href='/icons/sprite.svg#download'/></svg>", UiRibbonItemSize.Small, Loading, null, "ExportCsv", new Func<Task>(()=>{ RaiseUiActionRequested("ExportCsv"); return Task.CompletedTask; })),
                    new UiRibbonAction("ExportXlsx", localizer["Ribbon_ExportExcel"].Value, "<svg><use href='/icons/sprite.svg#download'/></svg>", UiRibbonItemSize.Small, Loading, null, "ExportXlsx", new Func<Task>(()=>{ RaiseUiActionRequested("ExportXlsx"); return Task.CompletedTask; }))
                })
            };

            var registers = new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
            return registers.Count == 0 ? null : registers;
        }
    }
}

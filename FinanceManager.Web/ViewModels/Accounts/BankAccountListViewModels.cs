using FinanceManager.Shared;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Accounts
{
    /// <summary>
    /// List view model providing paging and rendering logic for bank accounts.
    /// </summary>
    public sealed class BankAccountListViewModel : BaseListViewModel<AccountListItem>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BankAccountListViewModel"/> class.
        /// </summary>
        /// <param name="sp">Service provider used to resolve required services (API client, localizer, etc.).</param>
        public BankAccountListViewModel(IServiceProvider sp) : base(sp)
        {
        }

        /// <summary>
        /// Indicates whether range/date filtering is supported by this list. Accounts list does not allow range filtering.
        /// </summary>
        public override bool AllowRangeFiltering => false;

        private int _skip;
        private const int PageSize = 50;

        /// <summary>
        /// Loads a page of account items from the API and appends them to the internal item collection.
        /// </summary>
        /// <param name="resetPaging">When <c>true</c> the paging offset is reset and the list will be rebuilt from the first page.</param>
        /// <returns>A task that completes when the page load has finished. Errors from the API are captured via <see cref="SetError"/> and do not propagate.</returns>
        protected override async Task LoadPageAsync(bool resetPaging)
        {
            var api = ServiceProvider.GetRequiredService<IApiClient>();
            try
            {
                if (resetPaging) { _skip = 0; }
                var list = await api.GetAccountsAsync(_skip, PageSize, null);
                var items = (list ?? Array.Empty<AccountDto>())
                    .Where(a => string.IsNullOrWhiteSpace(Search)
                        || (a.Name?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (a.Iban?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false))
                    .Select(a => new AccountListItem(a.Id, a.Name ?? string.Empty, a.Type.ToString(), a.Iban, a.CurrentBalance, a.SymbolAttachmentId));
                if (resetPaging) Items.Clear();
                Items.AddRange(items);
                _skip += PageSize;
                CanLoadMore = list != null && list.Count >= PageSize;
            }
            catch (Exception ex)
            {
                SetError(api.LastErrorCode ?? null, api.LastError ?? ex.Message);
                CanLoadMore = false;
            }
        }

        /// <summary>
        /// Builds the list column definitions and list records used by the UI to render the accounts table.
        /// </summary>
        protected override void BuildRecords()
        {
            var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();
            Columns = new List<ListColumn>
            {
                new ListColumn("symbol", string.Empty, "48px", ListColumnAlign.Left),
                new ListColumn("name", L["List_Th_Account_Name"], "18%", ListColumnAlign.Left),
                new ListColumn("type", L["List_Th_Account_Type"], "14%", ListColumnAlign.Left),
                new ListColumn("iban", L["List_Th_Account_Iban"], "26%", ListColumnAlign.Left),
                new ListColumn("balance", L["List_Th_Account_Balance"], "12%", ListColumnAlign.Right),
                new ListColumn("placeholder", string.Empty, ""),
            };
            Records = Items.Select(i => new ListRecord(new List<ListCell>
            {
                new ListCell(ListCellKind.Symbol, SymbolId: i.SymbolId),
                new ListCell(ListCellKind.Text, Text: i.Name),
                new ListCell(ListCellKind.Text, Text: i.Type),
                new ListCell(ListCellKind.Text, Text: string.IsNullOrWhiteSpace(i.Iban) ? "-" : i.Iban ?? string.Empty),
                new ListCell(ListCellKind.Currency, Amount: i.CurrentBalance),
                new ListCell(ListCellKind.Text),
            }, i)).ToList();
        }

        /// <summary>
        /// Returns the ribbon/action definitions shown for this list view. The provided <paramref name="localizer"/> is used to localize labels.
        /// </summary>
        /// <param name="localizer">Localizer used to resolve UI labels for the ribbon actions.</param>
        /// <returns>A list of <see cref="UiRibbonRegister"/> describing available ribbon tabs and actions.</returns>
        protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
        {
            var actions = new List<UiRibbonAction>
            {
                new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Small, false, null, "Back", () => { RaiseUiActionRequested("Back"); return Task.CompletedTask; }),
                new UiRibbonAction("New", localizer["Ribbon_New"].Value, "<svg><use href='/icons/sprite.svg#plus'/></svg>", UiRibbonItemSize.Small, false, null, "New", () => { RaiseUiActionRequested("New"); return Task.CompletedTask; }),
                new UiRibbonAction("ClearSearch", localizer["Ribbon_ClearSearch"].Value, "<svg><use href='/icons/sprite.svg#clear'/></svg>", UiRibbonItemSize.Small, false, null, "ClearSearch", () => { RaiseUiActionRequested("ClearSearch"); return Task.CompletedTask; })
            };
            var tabs = new List<UiRibbonTab> { new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, actions) };
            return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
        }
    }
}

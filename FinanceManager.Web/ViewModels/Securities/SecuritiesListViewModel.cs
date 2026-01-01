using FinanceManager.Shared.Dtos;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Components;
using FinanceManager.Web.Extensions;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Shared.Dtos.Postings;

namespace FinanceManager.Web.ViewModels.Securities;

/// <summary>
/// List view model for securities. Responsible for loading securities, preparing display symbol mapping
/// and building list records for UI rendering.
/// </summary>
public sealed partial class SecuritiesListViewModel : BaseListViewModel<SecurityListItem>
{
    private readonly Shared.IApiClient _api;
    private readonly NavigationManager _nav;

    /// <summary>
    /// Initializes a new instance of <see cref="SecuritiesListViewModel"/>.
    /// </summary>
    /// <param name="sp">Service provider used to resolve required services such as the API client and navigation manager.</param>
    public SecuritiesListViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<Shared.IApiClient>();
        _nav = sp.GetRequiredService<NavigationManager>();
    }

    /// <summary>
    /// When true only active securities are loaded; when false archived securities are included as well.
    /// </summary>
    public bool OnlyActive { get; private set; } = true;

    // mapping securityId -> display symbol attachment id (security symbol or category fallback)
    private readonly Dictionary<Guid, Guid?> _displaySymbolBySecurity = new();

    /// <summary>
    /// Loads the securities list from the API and prepares internal caches used for rendering.
    /// On failure the items collection is cleared and the view state is updated.
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the operation.</param>
    /// <returns>A task that completes when loading has finished.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the provided cancellation token requests cancellation.</exception>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!IsAuthenticated) { return; }
        try
        {
            var list = await _api.Securities_ListAsync(OnlyActive, ct);
            Items.Clear();
            Items.AddRange(list.Select(s => new SecurityListItem(s.Id, s.Name ?? string.Empty, s.Identifier ?? string.Empty, s.AlphaVantageCode, s.CategoryId, s.CategoryName, s.IsActive, s.SymbolAttachmentId)));
        }
        catch
        {
            Items.Clear();
            _displaySymbolBySecurity.Clear();
            RaiseStateChanged();
            return;
        }

        // Load categories to get category symbol fallbacks
        var categorySymbolMap = new Dictionary<Guid, Guid?>();
        try
        {
            var clist = await _api.SecurityCategories_ListAsync(ct);
            foreach (var c in clist)
            {
                if (c.Id != Guid.Empty)
                {
                    categorySymbolMap[c.Id] = c.SymbolAttachmentId;
                }
            }
        }
        catch { }

        _displaySymbolBySecurity.Clear();
        foreach (var s in Items)
        {
            Guid? display = null;
            if (s.SymbolId.HasValue)
            {
                display = s.SymbolId;
            }
            else if (s.CategoryId.HasValue)
            {
                if (categorySymbolMap.TryGetValue(s.CategoryId.Value, out var catSym) && catSym.HasValue)
                {
                    display = catSym;
                }
            }
            _displaySymbolBySecurity[s.Id] = display;
        }

        RaiseStateChanged();
    }

    /// <summary>
    /// Toggles the OnlyActive filter and reinitializes the view model.
    /// </summary>
    public void ToggleActive()
    {
        OnlyActive = !OnlyActive;
        _ = InitializeAsync();
        RaiseStateChanged();
    }

    /// <summary>
    /// Loads a page of securities. This implementation delegates to <see cref="LoadAsync"/>
    /// which currently loads the full list and disables further paging.
    /// </summary>
    /// <param name="resetPaging">When true reset paging state; ignored by the current implementation.</param>
    protected override async Task LoadPageAsync(bool resetPaging)
    {
        // For generic list provider load we delegate to LoadAsync which fills Items fully
        await LoadAsync();
        CanLoadMore = false;
    }

    /// <summary>
    /// Builds the list columns and records used by the UI renderer from the current <see cref="Items"/> collection.
    /// </summary>
    protected override void BuildRecords()
    {
        var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();
        Columns = new List<ListColumn>
        {
            new ListColumn("symbol", string.Empty, "56px", ListColumnAlign.Left),
            new ListColumn("name", L["List_Th_Name"], "", ListColumnAlign.Left),
            new ListColumn("identifier", L["List_Th_AlphaVantage"], "", ListColumnAlign.Left),
            new ListColumn("alphavantage", L["List_Th_Category"], "", ListColumnAlign.Left),
            new ListColumn("category", L["List_Th_Status"], "120px", ListColumnAlign.Left),
            new ListColumn("status", L["List_Th_Status"], "120px", ListColumnAlign.Left)
        };

        Records = Items.Select(i => new ListRecord(new List<ListCell>
        {
            new ListCell(ListCellKind.Symbol, SymbolId: _displaySymbolBySecurity.TryGetValue(i.Id, out var sym) ? sym : null),
            new ListCell(ListCellKind.Text, Text: i.Name),
            new ListCell(ListCellKind.Text, Text: i.Identifier),
            new ListCell(ListCellKind.Text, Text: string.IsNullOrWhiteSpace(i.AlphaVantageCode) ? "-" : i.AlphaVantageCode),
            new ListCell(ListCellKind.Text, Text: string.IsNullOrWhiteSpace(i.CategoryName) ? "-" : i.CategoryName),
            new ListCell(ListCellKind.Text, Text: i.IsActive ? (ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>()?[
                "StatusActive"].Value ?? "Active") : (ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>()?["StatusArchived"].Value ?? "Archived"))
        }, i)).ToList();
    }

    /// <summary>
    /// Builds ribbon register definitions for the securities list including actions and filter toggles.
    /// </summary>
    /// <param name="localizer">Localizer used to resolve UI labels.</param>
    /// <returns>Collection of ribbon registers describing available tabs and actions.</returns>
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        // Actions tab
        var actions = new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "New",
                localizer["Ribbon_New"].Value,
                "<svg><use href='/icons/sprite.svg#plus'/></svg>",
                UiRibbonItemSize.Large,
                false,
                null,
                "New",
                new Func<Task>(() => { RaiseUiActionRequested("New"); return Task.CompletedTask; })
            ),
            new UiRibbonAction(
                "Categories",
                localizer["Ribbon_Categories"].Value,
                "<svg><use href='/icons/sprite.svg#groups'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                "Categories",
                () => { RaiseUiActionRequested("OpenCategories"); return Task.CompletedTask; }
            )
        };

        // Filter tab - ToggleActive can be handled directly by VM via Callback
        var filter = new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "ToggleActive",
                localizer["Ribbon_ToggleActive"].Value,
                "<svg><use href='/icons/sprite.svg#check'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                null,
                new Func<Task>(() => { ToggleActive(); return Task.CompletedTask; })
            )
        };

        var tabsActions = new List<UiRibbonTab> { new UiRibbonTab(localizer["Ribbon_Group_Actions"].Value, actions) };
        var tabsFilter = new List<UiRibbonTab> { new UiRibbonTab(localizer["Ribbon_Group_Filter"].Value, filter) };

        return new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabsActions),
            new UiRibbonRegister(UiRibbonRegisterKind.Custom, tabsFilter)
        };
    }

    /// <summary>
    /// Returns the attachment id used as display symbol for the provided security or category fallback.
    /// </summary>
    /// <param name="security">List item representing the security. May be <c>null</c>.</param>
    /// <returns>Attachment id to use for the symbol or <c>null</c> when none is available.</returns>
    public Guid? GetDisplaySymbolAttachmentId(SecurityListItem security)
    {
        if (security == null) return null;
        return _displaySymbolBySecurity.TryGetValue(security.Id, out var v) ? v : null;
    }
}

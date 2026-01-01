using DocumentFormat.OpenXml.Office2010.Excel;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Web.ViewModels.Securities.Prices;

/// <summary>
/// List view model that provides paging and rendering for historical prices of a single security.
/// The view model pages prices from the API and supports an optional client-side range filter.
/// </summary>
public sealed class SecurityPricesListViewModel : BaseListViewModel<SecurityPriceDto>
{
    private readonly Shared.IApiClient _api;
    private readonly IStringLocalizer<Pages> _L;
    private readonly NavigationManager _nav;
    private const int PageSize = 100;

    private int _rawFetchedCount = 0; // number of items fetched from server (unfiltered)

    /// <summary>
    /// Identifier of the security whose prices are displayed.
    /// </summary>
    public Guid SecurityId { get; }

    /// <summary>
    /// Disables the generic search UI for the prices list (search is not supported server-side for this list).
    /// </summary>
    public override bool AllowSearchFiltering { get => false; protected set => base.AllowSearchFiltering = false; }

    /// <summary>
    /// Initializes a new instance of <see cref="SecurityPricesListViewModel"/>.
    /// </summary>
    /// <param name="sp">Service provider used to resolve required services.</param>
    /// <param name="securityId">Identifier of the security to load prices for.</param>
    public SecurityPricesListViewModel(IServiceProvider sp, Guid securityId) : base(sp)
    {
        _api = sp.GetRequiredService<Shared.IApiClient>();
        _L = sp.GetRequiredService<IStringLocalizer<Pages>>();
        _nav = sp.GetRequiredService<NavigationManager>();
        SecurityId = securityId;
        CanLoadMore = true;
        _rawFetchedCount = 0;
    }

    /// <summary>
    /// Loads a page of prices from the API. This method implements paging by requesting chunks of <see cref="PageSize"/> items
    /// and applying an optional client-side date range filter via <see cref="ApplyRangeFilter(IReadOnlyList{SecurityPriceDto})"/>.
    /// </summary>
    /// <param name="resetPaging">When true the paging state is reset and previously loaded items are cleared.</param>
    /// <returns>A task that completes when the page has been loaded.</returns>
    protected override async Task LoadPageAsync(bool resetPaging)
    {
        if (!CheckAuthentication()) return;
        if (resetPaging)
        {
            Items.Clear();
            CanLoadMore = true;
            _rawFetchedCount = 0;
        }

        if (!CanLoadMore) return;

        try
        {
            var skip = _rawFetchedCount;
            var chunk = await _api.Securities_GetPricesAsync(SecurityId, skip, PageSize, CancellationToken.None) ?? Array.Empty<SecurityPriceDto>();
            _rawFetchedCount += chunk.Count;

            // Apply optional range filter (client-side because API currently does not expose from/to)
            var filtered = ApplyRangeFilter(chunk);
            Items.AddRange(filtered);

            // If server returned less than page size, there are no further pages
            CanLoadMore = chunk.Count >= PageSize;
        }
        catch
        {
            // On error stop loading
            CanLoadMore = false;
        }
    }

    /// <summary>
    /// Applies a client-side date range filter to a chunk of prices.
    /// </summary>
    /// <param name="chunk">Chunk of prices returned from the API (unfiltered).</param>
    /// <returns>Filtered enumerable that respects <see cref="RangeFrom"/> and <see cref="RangeTo"/> if provided.</returns>
    private IEnumerable<SecurityPriceDto> ApplyRangeFilter(IReadOnlyList<SecurityPriceDto> chunk)
    {
        if (!RangeFrom.HasValue && !RangeTo.HasValue) return chunk;

        var from = RangeFrom?.Date;
        var to = RangeTo?.Date;
        return chunk.Where(p =>
        {
            var d = p.Date.Date;
            if (from.HasValue && d < from.Value) return false;
            if (to.HasValue && d > to.Value) return false;
            return true;
        }).ToList();
    }

    /// <summary>
    /// Builds the list columns and record rows for the UI renderer.
    /// The date column uses <see cref="System.Globalization.CultureInfo.CurrentCulture"/> and the close price is formatted accordingly.
    /// </summary>
    protected override void BuildRecords()
    {
        Columns = new List<ListColumn>
        {
            new ListColumn("date", _L["List_Th_Date"], "120px", ListColumnAlign.Left),
            new ListColumn("close", _L["List_Th_Close"], "120px", ListColumnAlign.Right)
        };

        Records = Items.Select(p => new ListRecord(new List<ListCell>
        {
            new ListCell(ListCellKind.Text, Text: p.Date.ToShortDateString()),
            new ListCell(ListCellKind.Text, Text: p.Close.ToString(System.Globalization.CultureInfo.CurrentCulture))
        }, p)).ToList();
    }

    /// <summary>
    /// Requests opening of the backfill overlay for this security. The overlay parameters include the <see cref="SecurityId"/>.
    /// </summary>
    public void RequestOpenBackfill()
    {
        var parameters = new Dictionary<string, object?> { ["SecurityId"] = SecurityId };
        var spec = new BaseViewModel.UiOverlaySpec(typeof(Components.Shared.SecurityPricesBackfillPanel), parameters);
        RaiseUiActionRequested("OpenOverlay", spec);
    }

    /// <summary>
    /// Builds ribbon register definitions for the list page including navigation (Back) and a Manage action to backfill prices.
    /// </summary>
    /// <param name="localizer">Localizer used to resolve UI labels.</param>
    /// <returns>Collection of ribbon registers describing available tabs and actions for the UI.</returns>
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var tabs = new List<UiRibbonTab>
        {
            new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, new List<UiRibbonAction>
            {
                new UiRibbonAction(
                    "Back",
                    localizer["Ribbon_Back"].Value,
                    "<svg><use href='/icons/sprite.svg#back'/></svg>",
                    UiRibbonItemSize.Large,
                    false,
                    null,
                    "Back",
                    () => { _nav.NavigateTo($"/card/securities/{SecurityId}"); return Task.CompletedTask; }
                )
            }),

            new UiRibbonTab(localizer["Ribbon_Group_Manage"].Value, new List<UiRibbonAction>
            {
                new UiRibbonAction(
                    "BackfillPrices",
                    localizer["Ribbon_Backfill"].Value,
                    "<svg><use href='/icons/sprite.svg#postings'/></svg>",
                    UiRibbonItemSize.Small,
                    SecurityId == Guid.Empty,
                    null,
                    "Backfill",
                    () => { RequestOpenBackfill(); return Task.CompletedTask; }
                )
            })
        };

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }
}

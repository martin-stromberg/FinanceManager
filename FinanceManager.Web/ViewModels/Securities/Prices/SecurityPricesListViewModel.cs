using DocumentFormat.OpenXml.Office2010.Excel;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Web.ViewModels.Securities.Prices;

public sealed class SecurityPricesListViewModel : BaseListViewModel<SecurityPriceDto>
{
    private readonly Shared.IApiClient _api;
    private readonly IStringLocalizer<Pages> _L;
    private readonly NavigationManager _nav;
    private const int PageSize = 100;

    private int _rawFetchedCount = 0; // number of items fetched from server (unfiltered)

    public Guid SecurityId { get; }

    public override bool AllowSearchFiltering { get => false; protected set => base.AllowSearchFiltering = false; }

    public SecurityPricesListViewModel(IServiceProvider sp, Guid securityId) : base(sp)
    {
        _api = sp.GetRequiredService<Shared.IApiClient>();
        _L = sp.GetRequiredService<IStringLocalizer<Pages>>();
        _nav = sp.GetRequiredService<NavigationManager>();
        SecurityId = securityId;
        CanLoadMore = true;
        _rawFetchedCount = 0;
    }

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

    // Expose a helper to open backfill via UI action if desired
    public void RequestOpenBackfill()
    {
        var parameters = new Dictionary<string, object?> { ["SecurityId"] = SecurityId };
        var spec = new BaseViewModel.UiOverlaySpec(typeof(Components.Shared.SecurityPricesBackfillPanel), parameters);
        RaiseUiActionRequested("OpenOverlay", spec);
    }

    // Provide ribbon actions for the list page: Navigation (Back) and Manage (Backfill prices)
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

using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Securities;

public sealed class SecuritiesListViewModel : ViewModelBase
{
    private readonly Shared.IApiClient _api;

    public SecuritiesListViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<Shared.IApiClient>();
    }

    public bool Loaded { get; private set; }
    public List<SecurityDto> Items { get; private set; } = new();
    public bool OnlyActive { get; private set; } = true;

    // mapping securityId -> display symbol attachment id (security symbol or category fallback)
    private readonly Dictionary<Guid, Guid?> _displaySymbolBySecurity = new();

    public override async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        if (!IsAuthenticated)
        {
            RequireAuthentication(null);
            return;
        }
        await LoadAsync(ct);
        Loaded = true;
        RaiseStateChanged();
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!IsAuthenticated) { return; }
        try
        {
            var list = await _api.Securities_ListAsync(OnlyActive, ct);
            Items = list.ToList();
        }
        catch
        {
            Items = new();
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
            if (s.SymbolAttachmentId.HasValue)
            {
                display = s.SymbolAttachmentId;
            }
            else if (s.CategoryId.HasValue && categorySymbolMap.TryGetValue(s.CategoryId.Value, out var catSym) && catSym.HasValue)
            {
                display = catSym;
            }
            _displaySymbolBySecurity[s.Id] = display;
        }

        RaiseStateChanged();
    }

    public void ToggleActive()
    {
        OnlyActive = !OnlyActive;
        _ = InitializeAsync();
        RaiseStateChanged();
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
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
                null
            ),
            new UiRibbonAction(
                "Categories",
                localizer["Ribbon_Categories"].Value,
                "<svg><use href='/icons/sprite.svg#groups'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                "Categories",
                null
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

    // Public helper for UI to get display symbol attachment id (security symbol or category fallback)
    public Guid? GetDisplaySymbolAttachmentId(SecurityDto security)
    {
        if (security == null) return null;
        return _displaySymbolBySecurity.TryGetValue(security.Id, out var v) ? v : null;
    }
}

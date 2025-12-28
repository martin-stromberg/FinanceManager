using FinanceManager.Shared;

namespace FinanceManager.Web.ViewModels.Reports;

public sealed class ReportsHomeViewModel : ViewModelBase
{
    private readonly IApiClient _api;

    public ReportsHomeViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<IApiClient>();
    }

    public bool Loading { get; private set; }
    public List<ReportFavoriteDto> Favorites { get; } = new();

    public override async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        if (!IsAuthenticated)
        {
            RequireAuthentication(null);
            return;
        }
        await ReloadAsync(ct);
        RaiseStateChanged();
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        if (Loading) { return; }
        Loading = true; RaiseStateChanged();
        try
        {
            var list = await _api.Reports_ListFavoritesAsync(ct);
            Favorites.Clear();
            Favorites.AddRange(list.OrderBy(f => f.Name));
        }
        catch { }
        finally { Loading = false; RaiseStateChanged(); }
    }

    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(Microsoft.Extensions.Localization.IStringLocalizer localizer)
    {
        var actions = new List<UiRibbonAction>
        {
            new UiRibbonAction("Reload", localizer["Ribbon_Reload"].Value, "<svg><use href='/icons/sprite.svg#refresh'/></svg>", UiRibbonItemSize.Small, Loading, null, "Reload", null),
            new UiRibbonAction("NewReport", localizer["Ribbon_NewReport"].Value, "<svg><use href='/icons/sprite.svg#plus'/></svg>", UiRibbonItemSize.Large, false, null, "NewReport", null)
        };
        var tabs = new List<UiRibbonTab> { new UiRibbonTab(localizer["Ribbon_Group_Actions"].Value, actions) };
        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }
}

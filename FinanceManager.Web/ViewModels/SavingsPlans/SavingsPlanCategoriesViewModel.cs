using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.SavingsPlans;

public sealed class SavingsPlanCategoriesViewModel : ViewModelBase
{
    private readonly IApiClient _api;

    public SavingsPlanCategoriesViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<IApiClient>();
    }

    public bool Loaded { get; private set; }
    public List<CategoryItem> Categories { get; } = new();

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
        var list = await _api.SavingsPlanCategories_ListAsync(ct);
        Categories.Clear();
        Categories.AddRange(list.Select(c => new CategoryItem { Id = c.Id, Name = c.Name, SymbolAttachmentId = c.SymbolAttachmentId }).OrderBy(c => c.Name));
        RaiseStateChanged();
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(Microsoft.Extensions.Localization.IStringLocalizer localizer)
    {
        var actions = new List<UiRibbonAction>
        {
            new UiRibbonAction("New", localizer["Ribbon_New"].Value, "<svg><use href='/icons/sprite.svg#plus'/></svg>", UiRibbonItemSize.Large, false, null, "New", null),
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Small, false, null, "Back", null)
        };
        var tabs = new List<UiRibbonTab> { new UiRibbonTab(localizer["Ribbon_Group_Actions"].Value, actions) };
        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }

    public sealed class CategoryItem { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; public Guid? SymbolAttachmentId { get; set; } }
}

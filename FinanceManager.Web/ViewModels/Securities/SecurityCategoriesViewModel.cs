using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Securities;

public sealed class SecurityCategoriesViewModel : ViewModelBase
{
    private readonly Shared.IApiClient _api;

    public SecurityCategoriesViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<Shared.IApiClient>();
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
        var list = await _api.SecurityCategories_ListAsync(ct);
        Categories.Clear();
        Categories.AddRange(list.Select(c => new CategoryItem { Id = c.Id, Name = c.Name, SymbolAttachmentId = c.SymbolAttachmentId }).OrderBy(c => c.Name));
        RaiseStateChanged();
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        var actions = new List<UiRibbonAction>
        {
            new UiRibbonAction("New", localizer["Ribbon_New"].Value, "<svg><use href='/icons/sprite.svg#plus'/></svg>", UiRibbonItemSize.Large, false, null, "New", new Func<Task>(async () => { RaiseUiActionRequested("New"); await Task.CompletedTask; })),
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Small, false, null, "Back", new Func<Task>(async () => { RaiseUiActionRequested("Back"); await Task.CompletedTask; }))
        };

        var tab = new UiRibbonTab(localizer["Ribbon_Group_Actions"].Value, actions);
        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab> { tab }) };
    }

    public sealed class CategoryItem { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; public Guid? SymbolAttachmentId { get; set; } }
}

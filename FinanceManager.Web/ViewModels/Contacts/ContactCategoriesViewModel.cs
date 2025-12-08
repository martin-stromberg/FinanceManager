using FinanceManager.Shared; // IApiClient
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Web.ViewModels.Contacts;

public sealed class ContactCategoriesViewModel : ViewModelBase
{
    private readonly IApiClient _api;
    private readonly NavigationManager _nav;

    public ContactCategoriesViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<IApiClient>();
        _nav = sp.GetRequiredService<NavigationManager>();
    }

    public bool Loaded { get; private set; }
    public bool Busy { get; private set; }
    public string? Error { get; private set; }

    [Required, MinLength(2)]
    public string CreateName { get => _createName; set { if (_createName != value) { _createName = value; RaiseStateChanged(); } } }
    private string _createName = string.Empty;

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
        try
        {
            var list = await _api.ContactCategories_ListAsync(ct);
            Categories.Clear();
            Categories.AddRange(list.Select(l => new CategoryItem { Id = l.Id, Name = l.Name, SymbolAttachmentId = l.SymbolAttachmentId }).OrderBy(c => c.Name));
            RaiseStateChanged();
        }
        catch { }
    }

    public async Task CreateAsync(CancellationToken ct = default)
    {
        if (Busy) { return; }
        Busy = true; Error = null; RaiseStateChanged();
        // Ensure caller can observe Busy=true even if HTTP completes synchronously
        await Task.Yield();
        try
        {
            var created = await _api.ContactCategories_CreateAsync(new ContactCategoryCreateRequest(CreateName), ct);
            if (created is not null)
            {
                CreateName = string.Empty;
                await LoadAsync(ct);
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            Busy = false; RaiseStateChanged();
        }
    }

    // New unified ribbon API
    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        var tab = new UiRibbonTab(localizer["Ribbon_Tab_ContactCategories"], new List<UiRibbonAction>());

        tab.Items.Add(new UiRibbonAction(
            Id: "back",
            Label: localizer["Ribbon_Back"],
            IconSvg: "<svg><use href='/icons/sprite.svg#back'/></svg>",
            Size: UiRibbonItemSize.Large,
            Disabled: false,
            Tooltip: null,
            Action: "Back",
            Callback: () => { _nav.NavigateTo("/contacts"); return Task.CompletedTask; }
        ));

        tab.Items.Add(new UiRibbonAction(
            Id: "new",
            Label: localizer["Ribbon_New"],
            IconSvg: "<svg><use href='/icons/sprite.svg#plus'/></svg>",
            Size: UiRibbonItemSize.Large,
            Disabled: Busy,
            Tooltip: null,
            Action: "New",
            Callback: () => { _nav.NavigateTo("/contact-categories/new"); return Task.CompletedTask; }
        ));

        var register = new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab> { tab });
        return new List<UiRibbonRegister> { register };
    }

    public sealed class CategoryItem { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; public Guid? SymbolAttachmentId { get; set; } }
}

using FinanceManager.Shared; // IApiClient
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Web.ViewModels.Contacts;

public sealed class ContactCategoryDetailViewModel : ViewModelBase
{
    private readonly IApiClient _api;

    public ContactCategoryDetailViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<IApiClient>();
    }

    public Guid? Id { get; private set; }
    public bool IsEdit => Id.HasValue;
    public bool Loaded { get; private set; }
    public string? Error { get; private set; }

    public EditModel Model { get; } = new();

    public async Task InitializeAsync(Guid? id, CancellationToken ct = default)
    {
        Id = id;
        Error = null;
        if (IsEdit)
        {
            var dto = await _api.ContactCategories_GetAsync(Id!.Value, ct);
            if (dto is not null)
            {
                Model.Name = dto.Name ?? string.Empty;
                Model.SymbolAttachmentId = dto.SymbolAttachmentId;
            }
            else
            {
                Error = "ErrorNotFound";
            }
        }
        Loaded = true;
        RaiseStateChanged();
    }

    public async Task<bool> SaveAsync(CancellationToken ct = default)
    {
        Error = null;
        if (!IsEdit)
        {
            var created = await _api.ContactCategories_CreateAsync(new ContactCategoryCreateRequest(Model.Name), ct);
            if (created is null)
            {
                Error = "Error_Create";
                RaiseStateChanged();
                return false;
            }
            Id = created.Id;
            Model.SymbolAttachmentId = created.SymbolAttachmentId;
            return true;
        }
        else
        {
            var ok = await _api.ContactCategories_UpdateAsync(Id!.Value, new ContactCategoryUpdateRequest(Model.Name), ct);
            if (!ok)
            {
                Error = "Error_Update";
                RaiseStateChanged();
                return false;
            }
            return true;
        }
    }

    public async Task<bool> DeleteAsync(CancellationToken ct = default)
    {
        if (!IsEdit || Id is null) { return false; }
        var ok = await _api.ContactCategories_DeleteAsync(Id!.Value, ct);
        if (!ok)
        {
            Error = "Error_Delete";
            RaiseStateChanged();
            return false;
        }
        return true;
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(Microsoft.Extensions.Localization.IStringLocalizer localizer)
    {
        var navActions = new List<UiRibbonAction>
        {
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Large, false, null, "Back", new Func<Task>(async () => { RaiseUiActionRequested("Back"); await Task.CompletedTask; }))
        };
        var canSave = !string.IsNullOrWhiteSpace(Model.Name) && Model.Name.Trim().Length >= 2;
        var editActions = new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "Save",
                localizer["Ribbon_Save"].Value,
                "<svg><use href='/icons/sprite.svg#save'/></svg>",
                UiRibbonItemSize.Large,
                !canSave,
                null,
                "Save",
                new Func<Task>(async () =>
                {
                    var ok = await SaveAsync();
                    if (ok) { RaiseUiActionRequested("Saved"); }
                })),
            new UiRibbonAction(
                "Delete",
                localizer["Ribbon_Delete"].Value,
                "<svg><use href='/icons/sprite.svg#delete'/></svg>",
                UiRibbonItemSize.Small,
                !IsEdit,
                null,
                "Delete",
                new Func<Task>(async () =>
                {
                    var ok = await DeleteAsync();
                    if (ok) { RaiseUiActionRequested("Deleted"); }
                }))
        };

        var tabs = new List<UiRibbonTab>
        {
            new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, navActions),
            new UiRibbonTab(localizer["Ribbon_Group_Edit"].Value, editActions)
        };

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }

    public sealed class EditModel
    {
        [Required, MinLength(2)] public string Name { get; set; } = string.Empty;
        public Guid? SymbolAttachmentId { get; set; }
    }
}

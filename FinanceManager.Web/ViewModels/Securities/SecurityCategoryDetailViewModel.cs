using Microsoft.Extensions.Localization;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Web.ViewModels.Securities;

public sealed class SecurityCategoryDetailViewModel : ViewModelBase
{
    private readonly Shared.IApiClient _api;

    public SecurityCategoryDetailViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<Shared.IApiClient>();
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
            var dto = await _api.SecurityCategories_GetAsync(Id.Value, ct);
            if (dto is not null)
            {
                Model.Name = dto.Name ?? string.Empty;
                Model.SymbolAttachmentId = dto.SymbolAttachmentId;
            }
            else
            {
                Error = "Err_NotFound";
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
            var created = await _api.SecurityCategories_CreateAsync(new SecurityCategoryRequest { Name = Model.Name }, ct);
            if (created is null)
            {
                Error = _api.LastError;
                RaiseStateChanged();
                return false;
            }
            return true;
        }
        else
        {
            var updated = await _api.SecurityCategories_UpdateAsync(Id!.Value, new SecurityCategoryRequest { Name = Model.Name }, ct);
            if (updated is null)
            {
                Error = _api.LastError;
                RaiseStateChanged();
                return false;
            }
            return true;
        }
    }

    public async Task<bool> DeleteAsync(CancellationToken ct = default)
    {
        if (!IsEdit || Id is null) { return false; }
        var ok = await _api.SecurityCategories_DeleteAsync(Id.Value, ct);
        if (!ok)
        {
            Error = _api.LastError;
            RaiseStateChanged();
            return false;
        }
        return true;
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        var navTab = new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "Back",
                localizer["Ribbon_Back"].Value,
                "<svg><use href='/icons/sprite.svg#back'/></svg>",
                UiRibbonItemSize.Large,
                false,
                null,
                "Back",
                new Func<Task>(async () => { RaiseUiActionRequested("Back"); await Task.CompletedTask; }))
        });

        var canSave = !string.IsNullOrWhiteSpace(Model.Name) && Model.Name.Trim().Length >= 2;
        var editTab = new UiRibbonTab(localizer["Ribbon_Group_Edit"].Value, new List<UiRibbonAction>
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
                    if (ok)
                    {
                        RaiseUiActionRequested("Saved");
                    }
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
                    if (ok)
                    {
                        RaiseUiActionRequested("Deleted");
                    }
                }))
        });

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab> { navTab, editTab }) };
    }

    public sealed class EditModel
    {
        [Required, MinLength(2)]
        public string Name { get; set; } = string.Empty;
        public Guid? SymbolAttachmentId { get; set; }
    }
}

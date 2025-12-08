using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Web.ViewModels.SavingsPlans;

public sealed class SavingsPlanCategoryDetailViewModel : ViewModelBase
{
    private readonly IApiClient _api;

    public SavingsPlanCategoryDetailViewModel(IServiceProvider sp) : base(sp)
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
            var dto = await _api.SavingsPlanCategories_GetAsync(Id.Value, ct);
            if (dto is not null)
            {
                Model.Name = dto.Name ?? string.Empty;
                Model.SymbolAttachmentId = dto.SymbolAttachmentId;
            }
            else
            {
                Error = "Error_NotFound";
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
            var created = await _api.SavingsPlanCategories_CreateAsync(new SavingsPlanCategoryDto { Name = Model.Name }, ct);
            if (created is null)
            {
                Error = _api.LastError ?? "Error_Create";
                RaiseStateChanged();
                return false;
            }
            Id = created.Id;
            Model.SymbolAttachmentId = created.SymbolAttachmentId;
            return true;
        }
        else
        {
            var updated = await _api.SavingsPlanCategories_UpdateAsync(Id!.Value, new SavingsPlanCategoryDto { Id = Id!.Value, Name = Model.Name }, ct);
            if (updated is null)
            {
                Error = _api.LastError ?? "Error_Update";
                RaiseStateChanged();
                return false;
            }
            return true;
        }
    }

    public async Task<bool> DeleteAsync(CancellationToken ct = default)
    {
        if (!IsEdit || Id is null) { return false; }
        var ok = await _api.SavingsPlanCategories_DeleteAsync(Id.Value, ct);
        if (!ok)
        {
            Error = _api.LastError ?? "Error_Delete";
            RaiseStateChanged();
            return false;
        }
        return true;
    }

    // Ribbon: provide registers/tabs/actions via the new provider API
    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        var canSave = !string.IsNullOrWhiteSpace(Model.Name) && Model.Name.Trim().Length >= 2;

        var tabs = new List<UiRibbonTab>
        {
            new UiRibbonTab(localizer["Ribbon_Group_Navigation"], new List<UiRibbonAction>
            {
                new UiRibbonAction(
                    Id: "Back",
                    Label: localizer["Ribbon_Back"],
                    IconSvg: "<svg><use href='/icons/sprite.svg#back'/></svg>",
                    Size: UiRibbonItemSize.Large,
                    Disabled: false,
                    Tooltip: null,
                    Action: "Back",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("Back"); return Task.CompletedTask; })
                )
            }),

            new UiRibbonTab(localizer["Ribbon_Group_Edit"], new List<UiRibbonAction>
            {
                new UiRibbonAction(
                    Id: "Save",
                    Label: localizer["Ribbon_Save"],
                    IconSvg: "<svg><use href='/icons/sprite.svg#save'/></svg>",
                    Size: UiRibbonItemSize.Large,
                    Disabled: !canSave,
                    Tooltip: null,
                    Action: "Save",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("Save"); return Task.CompletedTask; })
                ),
                new UiRibbonAction(
                    Id: "Delete",
                    Label: localizer["Ribbon_Delete"],
                    IconSvg: "<svg><use href='/icons/sprite.svg#delete'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: !IsEdit,
                    Tooltip: null,
                    Action: "Delete",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("Delete"); return Task.CompletedTask; })
                )
            })
        };

        var registers = new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
        var baseRegs = base.GetRibbonRegisters(localizer);
        if (baseRegs != null) registers.AddRange(baseRegs);
        return registers;
    }

    public sealed class EditModel
    {
        [Required, MinLength(2)]
        public string Name { get; set; } = string.Empty;
        public Guid? SymbolAttachmentId { get; set; }
    }
}

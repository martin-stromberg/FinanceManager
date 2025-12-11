using Microsoft.Extensions.Localization;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Web.ViewModels.Securities;

public sealed class SecurityEditViewModel : ViewModelBase
{
    private readonly Shared.IApiClient _api;

    public SecurityEditViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<Shared.IApiClient>();
    }

    public Guid? Id { get; private set; }
    public bool IsEdit => Id.HasValue;
    public bool Loaded { get; private set; }
    public string? Error { get; private set; }

    public string? BackNav { get; private set; }
    public Guid? ReturnDraftId { get; private set; }
    public Guid? ReturnEntryId { get; private set; }
    public string? PrefillName { get; private set; }

    public EditModel Model { get; } = new();
    public DisplayModel Display { get; private set; } = new();
    public List<SecurityCategoryDto> Categories { get; private set; } = new();

    public async Task InitializeAsync(Guid? id, string? backNav, Guid? draftId, Guid? entryId, string? prefillName, CancellationToken ct = default)
    {
        Id = id; BackNav = backNav; ReturnDraftId = draftId; ReturnEntryId = entryId; PrefillName = prefillName;
        Error = null; Loaded = false; Display = new DisplayModel();
        await LoadCategoriesAsync(ct);
        if (IsEdit)
        {
            var dto = await _api.Securities_GetAsync(Id.Value, ct);
            if (dto != null)
            {
                Display = new DisplayModel { Id = dto.Id, IsActive = dto.IsActive, CategoryName = dto.CategoryName };
                Model.Name = dto.Name;
                Model.Identifier = dto.Identifier;
                Model.Description = dto.Description;
                Model.AlphaVantageCode = dto.AlphaVantageCode;
                Model.CurrencyCode = dto.CurrencyCode;
                Model.CategoryId = dto.CategoryId;
                Model.SymbolAttachmentId = dto.SymbolAttachmentId;
                Loaded = true;
            }
            else
            {
                Error = "ErrorNotFound";
            }
        }
        else
        {
            Display = new DisplayModel { IsActive = true };
            if (!string.IsNullOrWhiteSpace(PrefillName) && string.IsNullOrWhiteSpace(Model.Name))
            {
                Model.Name = PrefillName!;
            }
            Loaded = true;
        }
        RaiseStateChanged();
    }

    public async Task LoadCategoriesAsync(CancellationToken ct = default)
    {
        var list = await _api.SecurityCategories_ListAsync(ct);
        Categories = list.ToList();
    }

    public async Task<SecurityDto?> SaveAsync(CancellationToken ct = default)
    {
        Error = null;
        if (IsEdit)
        {
            var dto = await _api.Securities_UpdateAsync(Id!.Value, new SecurityRequest
            {
                Name = Model.Name,
                Identifier = Model.Identifier,
                Description = Model.Description,
                AlphaVantageCode = Model.AlphaVantageCode,
                CurrencyCode = Model.CurrencyCode,
                CategoryId = Model.CategoryId
            }, ct);
            if (dto == null)
            {
                Error = _api.LastError;
                RaiseStateChanged();
                return null;
            }
            RaiseStateChanged();
            return dto;
        }
        else
        {
            var dto = await _api.Securities_CreateAsync(new SecurityRequest
            {
                Name = Model.Name,
                Identifier = Model.Identifier,
                Description = Model.Description,
                AlphaVantageCode = Model.AlphaVantageCode,
                CurrencyCode = Model.CurrencyCode,
                CategoryId = Model.CategoryId
            }, ct);
            RaiseStateChanged();
            return dto;
        }
    }

    public async Task<bool> ArchiveAsync(CancellationToken ct = default)
    {
        if (!IsEdit || Id == null || !Display.IsActive) { return false; }
        var ok = await _api.Securities_ArchiveAsync(Id.Value, ct);
        if (!ok)
        {
            Error = _api.LastError;
            RaiseStateChanged();
            return false;
        }
        return true;
    }

    public async Task<bool> DeleteAsync(CancellationToken ct = default)
    {
        if (!IsEdit || Id == null || Display.IsActive) { return false; }
        var ok = await _api.Securities_DeleteAsync(Id.Value, ct);
        if (!ok)
        {
            Error = _api.LastError;
            RaiseStateChanged();
            return false;
        }
        return true;
    }

    // Ribbon: provide registers/tabs/actions via the new provider API
    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        var canSave = !string.IsNullOrWhiteSpace(Model.Name) && Model.Name.Trim().Length >= 2 && !string.IsNullOrWhiteSpace(Model.Identifier) && Model.Identifier.Trim().Length >= 3;

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
                    Id: "Archive",
                    Label: localizer["Ribbon_Archive"],
                    IconSvg: "<svg><use href='/icons/sprite.svg#archive'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: !(IsEdit && Loaded && Display.IsActive),
                    Tooltip: null,
                    Action: "Archive",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("Archive"); return Task.CompletedTask; })
                ),
                new UiRibbonAction(
                    Id: "Delete",
                    Label: localizer["Ribbon_Delete"],
                    IconSvg: "<svg><use href='/icons/sprite.svg#delete'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: !(IsEdit && Loaded && !Display.IsActive),
                    Tooltip: null,
                    Action: "Delete",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("Delete"); return Task.CompletedTask; })
                )
            }),

            new UiRibbonTab(localizer["Ribbon_Group_Related"], new List<UiRibbonAction>
            {
                new UiRibbonAction(
                    Id: "Postings",
                    Label: localizer["Ribbon_Postings"],
                    IconSvg: "<svg><use href='/icons/sprite.svg#postings'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: !IsEdit || !Loaded,
                    Tooltip: null,
                    Action: "Postings",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("Postings"); return Task.CompletedTask; })
                ),
                new UiRibbonAction(
                    Id: "Prices",
                    Label: localizer["Ribbon_Prices"],
                    IconSvg: "<svg><use href='/icons/sprite.svg#postings'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: !IsEdit || !Loaded,
                    Tooltip: null,
                    Action: "Prices",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("Prices"); return Task.CompletedTask; })
                ),
                new UiRibbonAction(
                    Id: "Attachments",
                    Label: localizer["Ribbon_Attachments"],
                    IconSvg: "<svg><use href='/icons/sprite.svg#attachment'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: !IsEdit || !Loaded,
                    Tooltip: null,
                    Action: "Attachments",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("Attachments"); return Task.CompletedTask; })
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
        [Required, MinLength(2)] public string Name { get; set; } = string.Empty;
        [Required, MinLength(3)] public string Identifier { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AlphaVantageCode { get; set; }
        public string CurrencyCode { get; set; } = "EUR";
        public Guid? CategoryId { get; set; }
        public Guid? SymbolAttachmentId { get; set; }
    }
    public sealed class DisplayModel
    {
        public Guid? Id { get; set; }
        public bool IsActive { get; set; }
        public string? CategoryName { get; set; }
    }
}

using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using FinanceManager.Domain.Attachments;

namespace FinanceManager.Web.ViewModels.SavingsPlans.Categories;

public sealed class SavingsPlanCategoryCardViewModel : BaseCardViewModel<(string Key, string Value)>, IDeletableViewModel
{
    private readonly IApiClient _api;

    public SavingsPlanCategoryCardViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<IApiClient>();
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid? SymbolId { get; private set; }

    public override string Title => Name ?? base.Title;

    public override async Task LoadAsync(Guid id)
    {
        Id = id;
        Loading = true; SetError(null, null); RaiseStateChanged();
        try
        {
            if (id == Guid.Empty)
            {
                Name = string.Empty; SymbolId = null;
            }
            else
            {
                var dto = await _api.SavingsPlanCategories_GetAsync(id);
                if (dto == null)
                {
                    SetError(_api.LastErrorCode ?? null, _api.LastError ?? "Not found");
                    CardRecord = new CardRecord(new List<CardField>());
                    Loading = false; RaiseStateChanged();
                    return;
                }
                Name = dto.Name ?? string.Empty;
                SymbolId = dto.SymbolAttachmentId;
            }

            CardRecord = BuildCardRecord(Name, SymbolId);
            ClearPendingChanges();
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message);
            CardRecord = new CardRecord(new List<CardField>());
        }
        finally { Loading = false; RaiseStateChanged(); }
    }

    private CardRecord BuildCardRecord(string name, Guid? symbolId)
    {
        var fields = new List<CardField>
        {
            new CardField("Card_Caption_SavingsPlanCategory_Name", CardFieldKind.Text, text: name ?? string.Empty, editable: true),
            new CardField("Card_Caption_SavingsPlanCategory_Symbol", CardFieldKind.Symbol, symbolId: symbolId, editable: true)
        };
        return new CardRecord(fields, new { Name = name, SymbolId = symbolId });
    }

    public override async Task<bool> SaveAsync()
    {
        // apply pending values
        if (CardRecord != null)
        {
            var nameField = CardRecord.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_SavingsPlanCategory_Name");
            if (nameField != null) Name = nameField.Text ?? string.Empty;
            var symField = CardRecord.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_SavingsPlanCategory_Symbol");
            if (symField != null) SymbolId = symField.SymbolId;
        }

        try
        {
            if (Id == Guid.Empty)
            {
                var created = await _api.SavingsPlanCategories_CreateAsync(new SavingsPlanCategoryDto { Name = Name });
                if (created == null) { SetError(_api.LastErrorCode ?? null, _api.LastError ?? "Create failed"); return false; }
                Id = created.Id; SymbolId = created.SymbolAttachmentId;
            }
            else
            {
                var updated = await _api.SavingsPlanCategories_UpdateAsync(Id, new SavingsPlanCategoryDto { Id = Id, Name = Name });
                if (updated == null) { SetError(_api.LastErrorCode ?? null, _api.LastError ?? "Update failed"); return false; }
                SymbolId = updated.SymbolAttachmentId;
            }
            await LoadAsync(Id);
            RaiseUiActionRequested("Saved", Id.ToString());
            ClearPendingChanges();
            return true;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message);
            return false;
        }
    }

    public override async Task<bool> DeleteAsync()
    {
        if (Id == Guid.Empty) return false;
        try
        {
            var ok = await _api.SavingsPlanCategories_DeleteAsync(Id);
            if (!ok) { SetError(_api.LastErrorCode ?? null, _api.LastError ?? "Delete failed"); return false; }
            RaiseUiActionRequested("Deleted");
            return true;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message);
            return false;
        }
    }

    public override async Task ReloadAsync() => await LoadAsync(Id);

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(Microsoft.Extensions.Localization.IStringLocalizer localizer)
    {
        var nav = new UiRibbonTab(localizer["Ribbon_Group_Navigation"], new List<UiRibbonAction>
        {
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Large, false, null, "Back", () => { RaiseUiActionRequested("Back"); return Task.CompletedTask; })
        });

        var canSave = CardRecord != null && HasPendingChanges;
        var manage = new UiRibbonTab(localizer["Ribbon_Group_Manage"], new List<UiRibbonAction>
        {
            new UiRibbonAction("Save", localizer["Ribbon_Save"].Value, "<svg><use href='/icons/sprite.svg#save'/></svg>", UiRibbonItemSize.Large, !canSave, null, "Save", async () => { await SaveAsync(); }),
            new UiRibbonAction("Delete", localizer["Ribbon_Delete"].Value, "<svg><use href='/icons/sprite.svg#delete'/></svg>", UiRibbonItemSize.Small, Id==Guid.Empty, null, "Delete", () => { RaiseUiActionRequested("Delete"); return Task.CompletedTask; })
        });

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>{nav, manage}) };
    }

    // Symbol support
    protected override (AttachmentEntityKind Kind, Guid ParentId) GetSymbolParent() => (AttachmentEntityKind.SavingsPlanCategory, Id == Guid.Empty ? Guid.Empty : Id);
    protected override bool IsSymbolUploadAllowed() => true;
    protected override async Task AssignNewSymbolAsync(Guid? attachmentId)
    {
        try
        {
            if (attachmentId.HasValue) await _api.SavingsPlanCategories_SetSymbolAsync(Id, attachmentId.Value);
            else await _api.SavingsPlanCategories_ClearSymbolAsync(Id);
            await LoadAsync(Id);
        }
        catch { }
    }
}

using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using FinanceManager.Domain.Attachments;
using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Web.ViewModels.Contacts.Groups;

[FinanceManager.Web.ViewModels.Common.CardRoute("contacts", "categories")]
public sealed class ContactGroupCardViewModel : BaseCardViewModel<(string Key, string Value)>, IDeletableViewModel
{

    public ContactGroupCardViewModel(IServiceProvider sp) : base(sp)
    {
    }

    public Guid Id { get; private set; }

    // Local edit model representing the contact category
    public EditModel Model { get; } = new();

    public override string Title => CardRecord?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_ContactCategory_Name")?.Text ?? (Model?.Name ?? base.Title);

    public override async Task LoadAsync(Guid id)
    {
        Id = id;
        Loading = true; SetError(null, null); RaiseStateChanged();
        try
        {
            if (id == Guid.Empty)
            {
                // new
                Model.Name = string.Empty;
                Model.SymbolAttachmentId = null;
            }
            else
            {
                var dto = await ApiClient.ContactCategories_GetAsync(id);
                if (dto is null)
                {
                    SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? "Not found");
                    CardRecord = new CardRecord(new List<CardField>());
                    Loading = false; RaiseStateChanged();
                    return;
                }
                Model.Name = dto.Name ?? string.Empty;
                Model.SymbolAttachmentId = dto.SymbolAttachmentId;
            }

            CardRecord = BuildCardRecord(Model.Name, Model.SymbolAttachmentId);
            ClearPendingChanges();
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? ex.Message);
            CardRecord = new CardRecord(new List<CardField>());
        }
        finally { Loading = false; RaiseStateChanged(); }
    }

    private CardRecord BuildCardRecord(string name, Guid? symbolId)
    {
        var fields = new List<CardField>
        {
            new CardField("Card_Caption_ContactCategory_Name", CardFieldKind.Text, text: name ?? string.Empty, editable: true),
            new CardField("Card_Caption_ContactCategory_Symbol", CardFieldKind.Symbol, symbolId: symbolId, editable: true)
        };
        return new CardRecord(fields, Model);
    }

    public override async Task<bool> SaveAsync()
    {
        // apply pending values from CardRecord into Model
        if (CardRecord != null)
        {
            var nameField = CardRecord.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_ContactCategory_Name");
            if (nameField != null) Model.Name = nameField.Text ?? string.Empty;
            var symField = CardRecord.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_ContactCategory_Symbol");
            if (symField != null) Model.SymbolAttachmentId = symField.SymbolId;
        }

        try
        {
            if (Id == Guid.Empty)
            {
                var created = await ApiClient.ContactCategories_CreateAsync(new ContactCategoryCreateRequest(Model.Name));
                if (created is null)
                {
                    SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? "Create failed");
                    return false;
                }
                Id = created.Id;
                Model.SymbolAttachmentId = created.SymbolAttachmentId;
            }
            else
            {
                var ok = await ApiClient.ContactCategories_UpdateAsync(Id, new ContactCategoryUpdateRequest(Model.Name));
                if (!ok)
                {
                    SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? "Update failed");
                    return false;
                }
            }

            await LoadAsync(Id);
            RaiseUiActionRequested("Saved", Id.ToString());
            ClearPendingChanges();
            return true;
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? ex.Message);
            return false;
        }
    }

    public override async Task<bool> DeleteAsync()
    {
        if (Id == Guid.Empty) return false;
        try
        {
            var ok = await ApiClient.ContactCategories_DeleteAsync(Id);
            if (!ok)
            {
                SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? "Delete failed");
                return false;
            }
            RaiseUiActionRequested("Deleted");
            return true;
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? ex.Message);
            return false;
        }
    }

    public override async Task ReloadAsync()
    {
        await LoadAsync(Id);
    }

    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(Microsoft.Extensions.Localization.IStringLocalizer localizer)
    {
        var nav = new UiRibbonTab(localizer["Ribbon_Group_Navigation"], new List<UiRibbonAction>
        {
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Large, false, null, "Back", () => { RaiseUiActionRequested("Back"); return Task.CompletedTask; })
        });

        var canSave = CardRecord != null && HasPendingChanges;

        var manage = new UiRibbonTab(localizer["Ribbon_Group_Manage"], new List<UiRibbonAction>
        {
            new UiRibbonAction("Save", localizer["Ribbon_Save"].Value, "<svg><use href='/icons/sprite.svg#save'/></svg>", UiRibbonItemSize.Large, !canSave, null, "Save", async () => { await SaveAsync(); }),
            new UiRibbonAction("Delete", localizer["Ribbon_Delete"].Value, "<svg><use href='/icons/sprite.svg#delete'/></svg>", UiRibbonItemSize.Small, Id == Guid.Empty, null, "Delete", () => { RaiseUiActionRequested("Delete"); return Task.CompletedTask; })
        });

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>{nav, manage}) };
    }

    // --- Symbol support hooks required by BaseCardViewModel ---
    protected override (AttachmentEntityKind Kind, Guid ParentId) GetSymbolParent() => (AttachmentEntityKind.ContactCategory, Id == Guid.Empty ? Guid.Empty : Id);

    protected override bool IsSymbolUploadAllowed() => true;

    protected override async Task AssignNewSymbolAsync(Guid? attachmentId)
    {
        try
        {
            if (attachmentId.HasValue)
            {
                await ApiClient.ContactCategories_SetSymbolAsync(Id, attachmentId.Value);
            }
            else
            {
                await ApiClient.ContactCategories_ClearSymbolAsync(Id);
            }
            await LoadAsync(Id);
        }
        catch
        {
            // ignore
        }
    }

    public sealed class EditModel
    {
        public string Name { get; set; } = string.Empty;
        public Guid? SymbolAttachmentId { get; set; }
    }
}

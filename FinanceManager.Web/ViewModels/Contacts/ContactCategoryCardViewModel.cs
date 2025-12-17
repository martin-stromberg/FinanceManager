using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Web.ViewModels.Contacts;

public sealed class ContactCategoryCardViewModel : BaseCardViewModel<(string Key, string Value)>, IDeletableViewModel
{
    private readonly ContactCategoryDetailViewModel _detail;
    private readonly IApiClient _api;

    public ContactCategoryCardViewModel(IServiceProvider sp) : base(sp)
    {
        _detail = ActivatorUtilities.CreateInstance<ContactCategoryDetailViewModel>(sp);
        _api = sp.GetRequiredService<IApiClient>();
    }

    public Guid Id { get; private set; }
    public override string Title => CardRecord?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_ContactCategory_Name")?.Text ?? (_detail.Model?.Name ?? base.Title);

    public override async Task LoadAsync(Guid id)
    {
        Id = id;
        Loading = true; SetError(null, null); RaiseStateChanged();
        try
        {
            await _detail.InitializeAsync(id == Guid.Empty ? null : id);
            // build card record from detail model
            CardRecord = BuildCardRecord(_detail.Model.Name, _detail.Model.SymbolAttachmentId);
            // clear pending changes
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
            new CardField("Card_Caption_ContactCategory_Name", CardFieldKind.Text, text: name ?? string.Empty, editable: true),
            new CardField("Card_Caption_ContactCategory_Symbol", CardFieldKind.Symbol, symbolId: symbolId, editable: true)
        };
        return new CardRecord(fields, _detail.Model);
    }

    public override async Task<bool> SaveAsync()
    {
        // apply pending values from CardRecord into detail.Model
        if (CardRecord != null)
        {
            var nameField = CardRecord.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_ContactCategory_Name");
            if (nameField != null) _detail.Model.Name = nameField.Text ?? string.Empty;
            var symField = CardRecord.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_ContactCategory_Symbol");
            if (symField != null) _detail.Model.SymbolAttachmentId = symField.SymbolId;
        }

        var ok = await _detail.SaveAsync();
        if (ok)
        {
            Id = _detail.Id ?? Id;
            // reload record to reflect persisted values (e.g., server normalized name)
            await LoadAsync(Id);
            RaiseUiActionRequested("Saved", Id.ToString());
            ClearPendingChanges();
            return true;
        }
        if (!string.IsNullOrWhiteSpace(_detail.Error)) SetError(null, _detail.Error);
        return false;
    }

    public override async Task<bool> DeleteAsync()
    {
        var ok = await _detail.DeleteAsync();
        if (ok)
        {
            RaiseUiActionRequested("Deleted");
            return true;
        }
        if (!string.IsNullOrWhiteSpace(_detail.Error)) SetError(null, _detail.Error);
        return false;
    }

    public override async Task<Guid?> ValidateSymbolAsync(System.IO.Stream stream, string fileName, string contentType)
    {
        try
        {
            var att = await _api.Attachments_UploadFileAsync((short)FinanceManager.Domain.Attachments.AttachmentEntityKind.ContactCategory, Id, stream, fileName, contentType ?? "application/octet-stream");
            // persist symbol onto category via service
            await _api.ContactCategories_SetSymbolAsync(Id, att.Id);
            await LoadAsync(Id);
            return att.Id;
        }
        catch
        {
            return null;
        }
    }

    public override async Task ReloadAsync()
    {
        await LoadAsync(Id);
    }

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
            new UiRibbonAction("Delete", localizer["Ribbon_Delete"].Value, "<svg><use href='/icons/sprite.svg#delete'/></svg>", UiRibbonItemSize.Small, Id == Guid.Empty, null, "Delete", () => { RaiseUiActionRequested("Delete"); return Task.CompletedTask; })
        });

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>{nav, manage}) };
    }
}

using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;
using FinanceManager.Domain.Attachments;

namespace FinanceManager.Web.ViewModels.Contacts;

public sealed class ContactCardViewModel : BaseCardViewModel<(string Key, string Value)>, IDeletableViewModel, ISymbolAssignableCard
{
    private readonly IApiClient _api;
    public ContactCardViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<IApiClient>();
    }

    public Guid Id { get; private set; }
    public ContactDto? Contact { get; private set; }
    public override string Title => Contact?.Name ?? base.Title;

    public override async Task LoadAsync(Guid id)
    {
        Id = id;
        Loading = true; SetError(null, null); RaiseStateChanged();
        try
        {
            if (id == Guid.Empty)
            {
                Contact = new ContactDto(Guid.Empty, string.Empty, ContactType.Organization, null, null, false, null);
                CardRecord = await BuildCardRecordAsync(Contact);
                return;
            }

            Contact = await _api.Contacts_GetAsync(id);
            if (Contact == null)
            {
                SetError(_api.LastErrorCode ?? null, _api.LastError ?? "Contact not found");
                CardRecord = new CardRecord(new List<CardField>());
                return;
            }
            CardRecord = await BuildCardRecordAsync(Contact);
        }
        catch (Exception ex)
        {
            CardRecord = new CardRecord(new List<CardField>());
            SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message);
        }
        finally { Loading = false; RaiseStateChanged(); }
    }
    /// <summary>
    /// Convenience helper for requesting the Contact Merge overlay from any ViewModel.
    /// Pages can render the supplied UiOverlaySpec generically (DynamicComponent).
    /// </summary>
    private void RequestOpenMerge(Guid sourceId, string sourceType)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Visible"] = true,
            ["SourceId"] = sourceId,
            ["SourceType"] = sourceType,
            // Provide explicit overlay title so pages render correct header for dynamic overlay
            ["OverlayTitle"] = Localizer?["Merge_Title"].Value ?? "Merge"
        };
        var spec = new UiOverlaySpec(typeof(ContactMergePanel), parameters);
        RaiseUiActionRequested("OpenMerge", spec);
    }

    private async Task<CardRecord> BuildCardRecordAsync(ContactDto c)
    {
        var categoryName = string.Empty;
        if (c.CategoryId.HasValue && c.CategoryId.Value != Guid.Empty)
        {
            try
            {
                var cats = await _api.ContactCategories_ListAsync();
                var cat = cats?.FirstOrDefault(x => x.Id == c.CategoryId.Value);
                if (cat != null) categoryName = cat.Name ?? string.Empty;
            }
            catch { }
        }

        var fields = new List<CardField>
        {
            new CardField("Card_Caption_Contact_Name", CardFieldKind.Text, text: c.Name ?? string.Empty, editable: true),
            new CardField("Card_Caption_Contact_Type", CardFieldKind.Text, text: c.Type.ToString(), editable: true, lookupType: "Enum:ContactType"),
            new CardField("Card_Caption_Contact_Category", CardFieldKind.Text, text: categoryName, editable: true, lookupType: "ContactCategory", valueId: c.CategoryId),
            new CardField("Card_Caption_Contact_Description", CardFieldKind.Text, text: c.Description ?? string.Empty, editable: true),
            new CardField("Card_Caption_Contact_Symbol", CardFieldKind.Symbol, symbolId: c.SymbolAttachmentId, editable: c.Id != Guid.Empty),
            new CardField("Card_Caption_Contact_IsPaymentIntermediary", CardFieldKind.Text, text: (c.IsPaymentIntermediary ? BooleanSelection.True:BooleanSelection.False).ToString(), editable: true, lookupType: "Enum:BooleanSelection")
        };

        var record = new CardRecord(fields, c);
        return ApplyPendingValues(ApplyEnumTranslations(record));
    }

    public override async Task<IReadOnlyList<LookupItem>> QueryLookupAsync(CardField field, string? q, int skip, int take)
    {
        try
        {
            if (string.Equals(field.LookupType, "ContactCategory", StringComparison.OrdinalIgnoreCase))
            {
                var list = await _api.ContactCategories_ListAsync();
                if (!string.IsNullOrWhiteSpace(q))
                {
                    var term = q.Trim();
                    return list.Where(c => !string.IsNullOrWhiteSpace(c.Name) && c.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                               .Select(c => new LookupItem(c.Id, c.Name)).ToList();
                }
                return list.Select(c => new LookupItem(c.Id, c.Name)).ToList();
            }
        }
        catch { }
        return await base.QueryLookupAsync(field, q, skip, take);
    }

    public override void ValidateLookupField(CardField field, LookupItem? item)
    {
        if (string.Equals(field.LookupType, "Enum:ContactType", StringComparison.OrdinalIgnoreCase))
        {
            field.Text = item?.Name ?? field.Text;
            ValidateFieldValue(field, field.Text);
            return;
        }
        base.ValidateLookupField(field, item);
    }

    public override async Task<Guid?> ValidateSymbolAsync(System.IO.Stream stream, string fileName, string contentType)
    {
        try
        {
            var att = await _api.Attachments_UploadFileAsync((short)FinanceManager.Domain.Attachments.AttachmentEntityKind.Contact, Id, stream, fileName, contentType ?? "application/octet-stream");
            await _api.Contacts_SetSymbolAsync(Id, att.Id);
            await InitializeAsync(Id);
            return att.Id;
        }
        catch { return null; }
    }

    public override async Task ReloadAsync()
    {
        await InitializeAsync(Id);
    }

    public override async Task<bool> DeleteAsync()
    {
        if (Contact == null) return false;
        Loading = true; SetError(null, null); RaiseStateChanged();
        try
        {
            var ok = await _api.Contacts_DeleteAsync(Id);
            if (!ok) { SetError(_api.LastErrorCode ?? null, _api.LastError ?? "Delete failed"); return false; }
            RaiseUiActionRequested("Deleted");
            return true;
        }
        catch (Exception ex) { SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message); return false; }
        finally { Loading = false; RaiseStateChanged(); }
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(Microsoft.Extensions.Localization.IStringLocalizer localizer)
    {
        var nav = new UiRibbonTab(localizer["Ribbon_Group_Navigation"], new List<UiRibbonAction>
        {
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Large, false, null, "Back", () => { RaiseUiActionRequested("Back"); return Task.CompletedTask; })
        });

        var manage = new UiRibbonTab(localizer["Ribbon_Group_Manage"], new List<UiRibbonAction>
        {
            new UiRibbonAction("Save", localizer["Ribbon_Save"].Value, "<svg><use href='/icons/sprite.svg#save'/></svg>", UiRibbonItemSize.Large, !HasPendingChanges, null, "Save", async () => {
                var dto = BuildDto(CardRecord);
                if (Id == Guid.Empty)
                {
                    var created = await _api.Contacts_CreateAsync(new ContactCreateRequest(dto.Name, dto.Type, dto.CategoryId, dto.Description, dto.IsPaymentIntermediary));
                    if (created != null) { Id = created.Id; Contact = created; CardRecord = await BuildCardRecordAsync(Contact); ClearPendingChanges(); RaiseStateChanged(); RaiseUiActionRequested("Saved", Id.ToString()); }
                }
                else
                {
                    var updated = await _api.Contacts_UpdateAsync(Id, new ContactUpdateRequest(dto.Name, dto.Type, dto.CategoryId, dto.Description, dto.IsPaymentIntermediary));
                    if (updated != null) { Contact = updated; CardRecord = await BuildCardRecordAsync(Contact); ClearPendingChanges(); RaiseStateChanged(); RaiseUiActionRequested("Saved", Id.ToString()); }
                }
            }),
            new UiRibbonAction("Delete", localizer["Ribbon_Delete"].Value, "<svg><use href='/icons/sprite.svg#delete'/></svg>", UiRibbonItemSize.Small, Contact==null, null, "Delete", () => { RaiseUiActionRequested("Delete"); return Task.CompletedTask; })
        });

        var linked = new UiRibbonTab(localizer["Ribbon_Group_Linked"], new List<UiRibbonAction>
        {
            new UiRibbonAction("OpenPostings", localizer["Ribbon_Postings"].Value, "<svg><use href='/icons/sprite.svg#postings'/></svg>", UiRibbonItemSize.Small, Contact==null, null, "OpenPostings", () => { var url = $"/list/postings/contact/{Id}"; RaiseUiActionRequested("OpenPostings", url); return Task.CompletedTask; }),
            new UiRibbonAction("OpenMerge", localizer["Ribbon_Merge"].Value, "<svg><use href='/icons/sprite.svg#merge'/></svg>", UiRibbonItemSize.Small, Contact==null, null, "OpenMerge", () => {
                RequestOpenMerge(Id, Contact?.Type.ToString() ?? string.Empty);
                return Task.CompletedTask;
            }),
            new UiRibbonAction("Attachments", localizer["Ribbon_Attachments"].Value, "<svg><use href='/icons/sprite.svg#attachment'/></svg>", UiRibbonItemSize.Small, Contact==null, null, "OpenAttachments", () => {
                RequestOpenAttachments(FinanceManager.Domain.Attachments.AttachmentEntityKind.Contact, Id);
                return Task.CompletedTask;
            })
        });

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>{nav, manage, linked}) };
    }

    private ContactDto BuildDto(CardRecord? record)
    {
        var name = record?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_Contact_Name")?.Text ?? Contact?.Name ?? string.Empty;
        var typeText = record?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_Contact_Type")?.Text ?? Contact?.Type.ToString() ?? string.Empty;
        ContactType type = Contact?.Type ?? ContactType.Organization;
        if (!string.IsNullOrWhiteSpace(typeText) && Enum.TryParse<ContactType>(typeText, true, out var parsed)) type = parsed;
        var categoryId = record?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_Contact_Category")?.ValueId ?? Contact?.CategoryId;
        var desc = record?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_Contact_Description")?.Text ?? Contact?.Description;
        var isPaymentText = record?.Fields.FirstOrDefault(f => f.LabelKey == "Card_Caption_Contact_IsPaymentIntermediary")?.Text;
        var isPayment = string.Equals(isPaymentText, Localizer?["EnumType_BooleanSelection_True"].Value, StringComparison.OrdinalIgnoreCase);
        return new ContactDto(Id, name, type, categoryId, desc, isPayment, Contact?.SymbolAttachmentId);
    }
}

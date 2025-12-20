using FinanceManager.Shared.Dtos;
using FinanceManager.Domain.Attachments;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Web.ViewModels.Securities;

public sealed class SecurityCardViewModel : BaseCardViewModel<(string Key, string Value)>
{
    private readonly Shared.IApiClient _api;

    public SecurityCardViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<Shared.IApiClient>();
    }

    public Guid Id { get; private set; }
    public SecurityDto? Security { get; private set; }
    public override string Title => Security?.Name ?? base.Title;

    // Compatibility with SecurityEdit page
    public sealed class EditModel
    {
        public string Name { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
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

    public EditModel Model { get; } = new();
    public DisplayModel Display { get; private set; } = new();
    public List<SecurityCategoryDto> Categories { get; private set; } = new();
    public string? Error { get; private set; }

    // Navigation context
    public string? BackNav { get; private set; }
    public Guid? ReturnDraftId { get; private set; }
    public Guid? ReturnEntryId { get; private set; }
    public string? PrefillName { get; private set; }

    public bool IsEdit => Id != Guid.Empty;

    public string ChartEndpoint => Id != Guid.Empty ? $"/api/securities/{Id}/aggregates" : string.Empty;

    public override AggregateBarChartViewModel? ChartViewModel
    {
        get
        {
            if (Id == Guid.Empty) return null;
            var title = Localizer?["Chart_Title_Security"] ?? Localizer?["General_Title_Securities"] ?? "Security";
            return new AggregateBarChartViewModel(ServiceProvider, ChartEndpoint, title);
        }
    }

    public override async Task LoadAsync(Guid id)
    {
        Id = id;
        Loading = true; SetError(null, null); RaiseStateChanged();
        try
        {
            if (id == Guid.Empty)
            {
                Security = new SecurityDto { Id = Guid.Empty };
                CardRecord = await BuildCardRecordAsync(null);
                return;
            }

            var dto = await _api.Securities_GetAsync(id);
            if (dto == null)
            {
                SetError(_api.LastErrorCode ?? null, _api.LastError ?? "Security not found");
                CardRecord = new CardRecord(new List<CardField>());
                return;
            }
            Id = dto.Id;
            Security = dto;
            CardRecord = await BuildCardRecordAsync(dto);
        }
        catch (Exception ex)
        {
            CardRecord = new CardRecord(new List<CardField>());
            SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message);
        }
        finally { Loading = false; RaiseStateChanged(); }
    }

    public async Task InitializeAsync(Guid? id, string? backNav = null, Guid? draftId = null, Guid? entryId = null, string? prefillName = null)
    {
        BackNav = backNav; ReturnDraftId = draftId; ReturnEntryId = entryId; PrefillName = prefillName;
        await LoadCategoriesAsync();
        await LoadAsync(id ?? Guid.Empty);
    }

    public async Task LoadCategoriesAsync(CancellationToken ct = default)
    {
        try { Categories = (await _api.SecurityCategories_ListAsync(ct)).ToList(); } catch { Categories = new(); }
    }

    public async Task<SecurityDto?> SaveAsync(CancellationToken ct = default)
    {
        Error = null; SetError(null, null);
        try
        {
            var req = BuildRequestFromModel();
            if (Id == Guid.Empty)
            {
                var dto = await _api.Securities_CreateAsync(req, ct);
                if (dto == null) { Error = _api.LastError; SetError(_api.LastErrorCode, _api.LastError); RaiseStateChanged(); return null; }
                Id = dto.Id; Security = dto; Display = new DisplayModel { Id = dto.Id, IsActive = dto.IsActive, CategoryName = dto.CategoryName }; Model.SymbolAttachmentId = dto.SymbolAttachmentId; CardRecord = await BuildCardRecordAsync(dto); RaiseUiActionRequested("Saved", Id.ToString()); return dto;
            }
            else
            {
                var dto = await _api.Securities_UpdateAsync(Id, req, ct);
                if (dto == null) { Error = _api.LastError; SetError(_api.LastErrorCode, _api.LastError); RaiseStateChanged(); return null; }
                Security = dto; Display = new DisplayModel { Id = dto.Id, IsActive = dto.IsActive, CategoryName = dto.CategoryName }; Model.SymbolAttachmentId = dto.SymbolAttachmentId; CardRecord = await BuildCardRecordAsync(dto); RaiseUiActionRequested("Saved", Id.ToString()); return dto;
            }
        }
        catch (Exception ex) { Error = _api.LastError ?? ex.Message; SetError(_api.LastErrorCode, _api.LastError ?? ex.Message); return null; }
        finally { RaiseStateChanged(); }
    }

    private SecurityRequest BuildRequestFromModel()
    {
        string GetFieldText(string key, string? fallback = null)
        {
            if (_pendingFieldValues.TryGetValue(key, out var pv))
            {
                if (pv != null)
                {
                    var t = pv.GetType();
                    var nameProp = t.GetProperty("Name");
                    if (nameProp != null)
                    {
                        return nameProp.GetValue(pv)?.ToString() ?? string.Empty;
                    }
                    return pv?.ToString() ?? string.Empty;
                }
            }
            var f = CardRecord?.Fields?.FirstOrDefault(x => x.LabelKey == key);
            if (f != null && !string.IsNullOrWhiteSpace(f.Text)) return f.Text;
            return fallback ?? string.Empty;
        }

        Guid? GetFieldGuidValue(string key, Guid? fallback = null)
        {
            if (_pendingFieldValues.TryGetValue(key, out var pv))
            {
                if (pv != null)
                {
                    var t = pv.GetType();
                    var keyProp = t.GetProperty("Key");
                    if (keyProp != null)
                    {
                        var val = keyProp.GetValue(pv);
                        if (val is Guid gg) return gg;
                        if (val is string s && Guid.TryParse(s, out var parsed)) return parsed;
                    }
                    if (pv is Guid g) return g;
                    if (pv is string s2 && Guid.TryParse(s2, out var parsed2)) return parsed2;
                }
            }
            var f = CardRecord?.Fields?.FirstOrDefault(x => x.LabelKey == key);
            if (f != null && f.ValueId != null && f.ValueId != Guid.Empty) return f.ValueId;
            return fallback;
        }

        var name = GetFieldText("Card_Caption_Security_Name", Model.Name);
        var identifier = GetFieldText("Card_Caption_Security_Identifier", Model.Identifier);
        var alpha = GetFieldText("Card_Caption_Security_AlphaVantage", Model.AlphaVantageCode);
        var currency = GetFieldText("Card_Caption_Security_Currency", Model.CurrencyCode ?? "EUR");
        var description = GetFieldText("Card_Caption_Security_Description", Model.Description);
        var categoryId = GetFieldGuidValue("Card_Caption_Security_Category", Model.CategoryId);

        return new SecurityRequest
        {
            Name = name,
            Identifier = identifier,
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
            AlphaVantageCode = string.IsNullOrWhiteSpace(alpha) ? null : alpha,
            CurrencyCode = string.IsNullOrWhiteSpace(currency) ? "EUR" : currency,
            CategoryId = categoryId
        };
    }

    private async Task<CardRecord> BuildCardRecordAsync(SecurityDto? dto)
    {
        var fields = new List<CardField>
        {
            new CardField("Card_Caption_Security_Name", CardFieldKind.Text, text: dto?.Name ?? string.Empty, editable: true),
            new CardField("Card_Caption_Security_Identifier", CardFieldKind.Text, text: dto?.Identifier ?? string.Empty, editable: true),
            new CardField("Card_Caption_Security_AlphaVantage", CardFieldKind.Text, text: dto?.AlphaVantageCode ?? string.Empty, editable: true),
            new CardField("Card_Caption_Security_Currency", CardFieldKind.Text, text: dto?.CurrencyCode ?? "EUR", editable: true),
            new CardField("Card_Caption_Security_Category", CardFieldKind.Text, text: dto?.CategoryName ?? string.Empty, editable: true, lookupType: "SecurityCategory", valueId: dto?.CategoryId),
            new CardField("Card_Caption_Security_Description", CardFieldKind.Text, text: dto?.Description ?? string.Empty, editable: true),
            new CardField("Card_Caption_Security_Symbol", CardFieldKind.Symbol, symbolId: dto?.SymbolAttachmentId, editable: dto?.Id != Guid.Empty)
        };

        var record = new CardRecord(fields, dto);
        return ApplyPendingValues(record);
    }

    // --- Ribbon provider ---
    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        // Only allow saving when there are pending changes and required fields (Name + Identifier) are present.
        // CardRecord already reflects pending overrides via ApplyPendingValues.
        var hasRequiredValues = CardRecord != null &&
                                CardRecord.Fields.Any(f => f.LabelKey == "Card_Caption_Security_Name" && !string.IsNullOrWhiteSpace(f.Text)) &&
                                CardRecord.Fields.Any(f => f.LabelKey == "Card_Caption_Security_Identifier" && !string.IsNullOrWhiteSpace(f.Text));
        var canSave = HasPendingChanges && hasRequiredValues;

        var tabs = new List<UiRibbonTab>
        {
            new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, new List<UiRibbonAction>
            {
                new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Large, false, null, "Back", () => { RaiseUiActionRequested("Back"); return Task.CompletedTask; })
            }),

            new UiRibbonTab(localizer["Ribbon_Group_Manage"].Value, new List<UiRibbonAction>
            {
                new UiRibbonAction("Save", localizer["Ribbon_Save"].Value, "<svg><use href='/icons/sprite.svg#save'/></svg>", UiRibbonItemSize.Large, !canSave, null, "Save", async () => { await SaveAsync(); }),
                new UiRibbonAction("Archive", localizer["Ribbon_Archive"].Value, "<svg><use href='/icons/sprite.svg#archive'/></svg>", UiRibbonItemSize.Small, !(Id != Guid.Empty && Security != null && Security.IsActive), null, "Archive", async () => { await ArchiveAsync(); }),
                new UiRibbonAction("Delete", localizer["Ribbon_Delete"].Value, "<svg><use href='/icons/sprite.svg#delete'/></svg>", UiRibbonItemSize.Small, !(Id != Guid.Empty && Security != null && !Security.IsActive), null, "Delete", async () => { await DeleteAsync(); })
            }),

            new UiRibbonTab(localizer["Ribbon_Group_Linked"].Value, new List<UiRibbonAction>
            {
                new UiRibbonAction("Postings", localizer["Ribbon_Postings"].Value, "<svg><use href='/icons/sprite.svg#postings'/></svg>", UiRibbonItemSize.Small, !(Id != Guid.Empty), null, "Postings", () => { var url = $"/list/postings/security/{Id}"; RaiseUiActionRequested("OpenPostings", url); return Task.CompletedTask; }),
                new UiRibbonAction("Prices", localizer["Ribbon_Prices"].Value, "<svg><use href='/icons/sprite.svg#postings'/></svg>", UiRibbonItemSize.Small, !(Id != Guid.Empty), null, "Prices", () => { var url = $"/list/securities/prices/{Id}"; RaiseUiActionRequested("OpenPrices", url); return Task.CompletedTask; }),
                new UiRibbonAction("Attachments", localizer["Ribbon_Attachments"].Value, "<svg><use href='/icons/sprite.svg#attachment'/></svg>", UiRibbonItemSize.Small, !(Id != Guid.Empty), null, "Attachments", () => { RequestOpenAttachments(AttachmentEntityKind.Security, Id); return Task.CompletedTask; })
            })
        };

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }

    public async Task<bool> ArchiveAsync()
    {
        if (Id == Guid.Empty) return false;
        Loading = true; SetError(null, null); RaiseStateChanged();
        try
        {
            var ok = await _api.Securities_ArchiveAsync(Id);
            if (!ok) { SetError(_api.LastErrorCode ?? null, _api.LastError ?? "Archive failed"); return false; }
            // reload to reflect archived state
            await LoadAsync(Id);
            RaiseUiActionRequested("Archived");
            return true;
        }
        catch (Exception ex) { SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message); return false; }
        finally { Loading = false; RaiseStateChanged(); }
    }

    public override async Task<bool> DeleteAsync()
    {
        if (Id == Guid.Empty) return false;
        Loading = true; SetError(null, null); RaiseStateChanged();
        try
        {
            var ok = await _api.Securities_DeleteAsync(Id);
            if (!ok) { SetError(_api.LastErrorCode ?? null, _api.LastError ?? "Delete failed"); return false; }
            RaiseUiActionRequested("Deleted");
            return true;
        }
        catch (Exception ex) { SetError(_api.LastErrorCode ?? null, _api.LastError ?? ex.Message); return false; }
        finally { Loading = false; RaiseStateChanged(); }
    }

    public override async Task<IReadOnlyList<LookupItem>> QueryLookupAsync(CardField field, string? q, int skip, int take)
    {
        if (!string.IsNullOrWhiteSpace(field.LookupType) && string.Equals(field.LookupType, "SecurityCategory", StringComparison.OrdinalIgnoreCase))
        {
            // Ensure categories are loaded
            if (Categories == null || Categories.Count == 0)
            {
                await LoadCategoriesAsync();
            }

            var list = Categories
                .Where(c => string.IsNullOrWhiteSpace(q) || c.Name.Contains(q ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Name)
                .Skip(Math.Max(0, skip))
                .Take(Math.Max(0, Math.Min(200, take)))
                .Select(c => new LookupItem(c.Id, c.Name))
                .ToList();

            return list;
        }

        return await base.QueryLookupAsync(field, q, skip, take);
    }

    protected override (AttachmentEntityKind Kind, Guid ParentId) GetSymbolParent() => (AttachmentEntityKind.Security, Id == Guid.Empty ? Guid.Empty : Id);
    protected override bool IsSymbolUploadAllowed() => Id != Guid.Empty;

    protected override async Task AssignNewSymbolAsync(Guid? attachmentId)
    {
        try
        {
            if (attachmentId.HasValue)
            {
                await _api.Securities_SetSymbolAsync(Id, attachmentId.Value);
            }
            else
            {
                await _api.Securities_ClearSymbolAsync(Id);
            }
            await LoadAsync(Id);
        }
        catch
        {
            // swallow
        }
    }
}

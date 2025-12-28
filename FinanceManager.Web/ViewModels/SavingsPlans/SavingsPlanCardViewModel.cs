using FinanceManager.Shared.Dtos.SavingsPlans;
using Microsoft.Extensions.Localization;
using FinanceManager.Domain.Attachments;
using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Web.ViewModels.SavingsPlans;

[FinanceManager.Web.ViewModels.Common.CardRoute("savings-plans")]
public sealed class SavingsPlanCardViewModel : BaseCardViewModel<(string Key, string Value)>
{

    public SavingsPlanCardViewModel(IServiceProvider sp) : base(sp)
    {
    }

    public Guid Id { get; private set; }
    public bool IsEdit => Id != Guid.Empty;

    public string? Error { get; private set; }
    public bool Loaded { get; private set; }

    public SavingsPlanAnalysisDto? Analysis { get; private set; }
    public List<SavingsPlanCategoryDto> Categories { get; private set; } = new();
    public override string Title => Model?.Name ?? base.Title;

    public SavingsPlanCreateRequest Model { get; private set; } = new(string.Empty, SavingsPlanType.OneTime, null, null, null, null, null);

    // Navigation context
    public Guid? ReturnDraftId { get; private set; }
    public Guid? ReturnEntryId { get; private set; }

    public string ChartEndpoint => IsEdit ? $"/api/savings-plans/{Id}/aggregates" : string.Empty;

    // remember last loaded dto to rebuild card with context
    private SavingsPlanDto? _loadedDto;

    public override AggregateBarChartViewModel? ChartViewModel
    {
        get
        {
            if (!IsEdit) return null;
            var title = Localizer?["Chart_Title_SavingsPlan_Aggregates"] ?? Localizer?["General_Title_SavingsPlans"] ?? "Savings Plan";
            var endpoint = ChartEndpoint;
            return new AggregateBarChartViewModel(ServiceProvider, endpoint, title);
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
                // If a prefill name was provided via InitializeAsync, apply it to the new model
                var initialName = !string.IsNullOrWhiteSpace(InitPrefill) ? InitPrefill : string.Empty;
                Model = new SavingsPlanCreateRequest(initialName, SavingsPlanType.OneTime, null, null, null, null, null);
                await LoadCategoriesAsync(); // ensure categories available for empty/new card
                _loadedDto = null;
                CardRecord = await BuildCardRecordAsync(null);
                return;
            }

            var dto = await ApiClient.SavingsPlans_GetAsync(id);
            if (dto == null)
            {
                SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? "Savings plan not found");
                CardRecord = new CardRecord(new List<CardField>());
                return;
            }
            Id = dto.Id;
            Model = new SavingsPlanCreateRequest(dto.Name, dto.Type, dto.TargetAmount, dto.TargetDate, dto.Interval, dto.CategoryId, dto.ContractNumber);

            // Load categories first so the category name can be resolved when building the card
            await LoadCategoriesAsync();

            _loadedDto = dto;
            CardRecord = await BuildCardRecordAsync(dto);
        }
        catch (Exception ex)
        {
            CardRecord = new CardRecord(new List<CardField>());
            SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? ex.Message);
        }
        finally { Loading = false; Loaded = true; RaiseStateChanged(); }
    }

    public async Task LoadAnalysisAsync(CancellationToken ct = default)
    {
        if (!IsEdit) { return; }
        try { Analysis = await ApiClient.SavingsPlans_AnalyzeAsync(Id, ct); } catch { }
        RaiseStateChanged();
    }

    public async Task LoadCategoriesAsync(CancellationToken ct = default)
    {
        try { Categories = (await ApiClient.SavingsPlanCategories_ListAsync(ct)).ToList(); } catch { Categories = new(); }
        RaiseStateChanged();
    }

    public async Task<bool> SaveAsync(CancellationToken ct = default)
    {
        SetError(null, null);
        try
        {
            // Apply any pending field edits into the Model so Save uses latest values
            var req = BuildCreateRequestFromPending();

            if (Id == Guid.Empty)
            {
                var dto = await ApiClient.SavingsPlans_CreateAsync(req, ct);
                if (dto == null) { SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? "Error_Create"); return false; }
                Id = dto.Id;
                Model = req; // reflect saved data into Model
                _loadedDto = dto;
                CardRecord = await BuildCardRecordAsync(dto);
                ClearPendingChanges();
                RaiseUiActionRequested("Saved", Id.ToString());
                return true;
            }
            else
            {
                var existing = await ApiClient.SavingsPlans_UpdateAsync(Id, req, ct);
                if (existing == null) { SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? "Error_Update"); return false; }
                Model = req; // reflect saved data into Model
                _loadedDto = existing;
                CardRecord = await BuildCardRecordAsync(existing);
                ClearPendingChanges();
                RaiseUiActionRequested("Saved", Id.ToString());
                return true;
            }
        }
        catch (Exception ex) { SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? ex.Message); return false; }
        finally { RaiseStateChanged(); }
    }

    // Build a SavingsPlanCreateRequest by merging current Model with any pending field overrides
    private SavingsPlanCreateRequest BuildCreateRequestFromPending()
    {
        var name = Model.Name;
        var type = Model.Type;
        decimal? targetAmount = Model.TargetAmount;
        DateTime? targetDate = Model.TargetDate;
        SavingsPlanInterval? interval = Model.Interval;
        Guid? categoryId = Model.CategoryId;
        string? contractNumber = Model.ContractNumber;

        if (_pendingFieldValues.TryGetValue("Card_Caption_SavingsPlan_Name", out var vName) && vName is string sName && !string.IsNullOrWhiteSpace(sName)) name = sName;

        if (_pendingFieldValues.TryGetValue("Card_Caption_SavingsPlan_Type", out var vType))
        {
            var s = vType?.ToString();
            if (!string.IsNullOrWhiteSpace(s))
            {
                if (Enum.TryParse<SavingsPlanType>(s, true, out var parsedType))
                {
                    type = parsedType;
                }
                else
                {
                    // try match against localized enum labels
                    try
                    {
                        foreach (var enumName in Enum.GetNames(typeof(SavingsPlanType)))
                        {
                            var key = $"EnumType_SavingsPlanType_{enumName}";
                            var localized = Localizer?[key];
                            var localizedVal = localized != null && !localized.ResourceNotFound ? localized.Value : null;
                            if (!string.IsNullOrWhiteSpace(localizedVal) && string.Equals(localizedVal, s, StringComparison.OrdinalIgnoreCase))
                            {
                                type = Enum.Parse<SavingsPlanType>(enumName);
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        if (_pendingFieldValues.TryGetValue("Card_Caption_SavingsPlan_TargetAmount", out var vAmt))
        {
            switch (vAmt)
            {
                case decimal d: targetAmount = d; break;
                case string ss when decimal.TryParse(ss, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out var pd): targetAmount = pd; break;
            }
        }

        if (_pendingFieldValues.TryGetValue("Card_Caption_SavingsPlan_TargetDate", out var vDate))
        {
            var s = vDate?.ToString();
            if (!string.IsNullOrWhiteSpace(s) && DateTime.TryParse(s, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out var pd)) targetDate = pd;
        }

        if (_pendingFieldValues.TryGetValue("Card_Caption_SavingsPlan_Interval", out var vInterval))
        {
            var s = vInterval?.ToString();
            if (!string.IsNullOrWhiteSpace(s))
            {
                if (Enum.TryParse<SavingsPlanInterval>(s, true, out var pi))
                {
                    interval = pi;
                }
                else
                {
                    // try match localized values
                    try
                    {
                        foreach (var enumName in Enum.GetNames(typeof(SavingsPlanInterval)))
                        {
                            var key = $"EnumType_SavingsPlanInterval_{enumName}";
                            var localized = Localizer?[key];
                            var localizedVal = localized != null && !localized.ResourceNotFound ? localized.Value : null;
                            if (!string.IsNullOrWhiteSpace(localizedVal) && string.Equals(localizedVal, s, StringComparison.OrdinalIgnoreCase))
                            {
                                interval = Enum.Parse<SavingsPlanInterval>(enumName);
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        if (_pendingFieldValues.TryGetValue("Card_Caption_SavingsPlan_Category", out var vCat))
        {
            if (vCat is LookupItem li) categoryId = li.Key;
            else if (vCat is Guid g) categoryId = g;
            else if (Guid.TryParse(vCat?.ToString() ?? string.Empty, out var pg)) categoryId = pg;
        }

        if (_pendingFieldValues.TryGetValue("Card_Caption_SavingsPlan_ContractNumber", out var vContract))
        {
            contractNumber = vContract?.ToString() ?? string.Empty;
        }

        return new SavingsPlanCreateRequest(name ?? string.Empty, type, targetAmount, targetDate, interval, categoryId, contractNumber);
    }

    public override async Task<bool> DeleteAsync()
    {
        if (Id == Guid.Empty) return false;
        Loading = true; SetError(null, null); RaiseStateChanged();
        try
        {
            var ok = await ApiClient.SavingsPlans_DeleteAsync(Id);
            if (!ok) { SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? "Delete failed"); return false; }
            RaiseUiActionRequested("Deleted");
            return true;
        }
        catch (Exception ex) { SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? ex.Message); return false; }
        finally { Loading = false; RaiseStateChanged(); }
    }

    private async Task<CardRecord> BuildCardRecordAsync(SavingsPlanDto? dto)
    {
        // Determine effective type taking pending changes into account so UI reflect immediate edits
        SavingsPlanType effectiveType = dto?.Type ?? Model.Type;
        if (_pendingFieldValues.TryGetValue("Card_Caption_SavingsPlan_Type", out var pendingType))
        {
            var s = pendingType?.ToString();
            if (!string.IsNullOrWhiteSpace(s))
            {
                if (Enum.TryParse<SavingsPlanType>(s, true, out var parsed))
                {
                    effectiveType = parsed;
                }
                else
                {
                    try
                    {
                        foreach (var enumName in Enum.GetNames(typeof(SavingsPlanType)))
                        {
                            var key = $"EnumType_SavingsPlanType_{enumName}";
                            var localized = Localizer?[key];
                            var localizedVal = localized != null && !localized.ResourceNotFound ? localized.Value : null;
                            if (!string.IsNullOrWhiteSpace(localizedVal) && string.Equals(localizedVal, s, StringComparison.OrdinalIgnoreCase))
                            {
                                effectiveType = Enum.Parse<SavingsPlanType>(enumName);
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        var intervalEditable = effectiveType == SavingsPlanType.Recurring;
        var showTargetDate = effectiveType != SavingsPlanType.Open; // Zieldatum only when not Unbefristet

        var fields = new List<CardField>
        {
            new CardField("Card_Caption_SavingsPlan_Name", CardFieldKind.Text, text: dto?.Name ?? Model.Name ?? string.Empty, editable: true),
            new CardField("Card_Caption_SavingsPlan_Category", CardFieldKind.Text, text: dto == null ? string.Empty : Categories.FirstOrDefault(c => c.Id == dto.CategoryId)?.Name ?? string.Empty, editable: true, lookupType: "Category", valueId: dto?.CategoryId),
            new CardField("Card_Caption_SavingsPlan_Type", CardFieldKind.Text, text: dto?.Type.ToString() ?? Model.Type.ToString(), editable: true, lookupType: "Enum:SavingsPlanType"),
        };
        if (intervalEditable)
        {
            fields.Add(new CardField("Card_Caption_SavingsPlan_Interval", CardFieldKind.Text, text: dto?.Interval?.ToString() ?? string.Empty, editable: true, lookupType: "Enum:SavingsPlanInterval"));
        }
        fields.Add(new CardField("Card_Caption_SavingsPlan_TargetAmount", CardFieldKind.Currency, amount: dto?.TargetAmount, editable: true));
        if (showTargetDate)
        {
            fields.Add(new CardField("Card_Caption_SavingsPlan_TargetDate", CardFieldKind.Text, text: dto?.TargetDate?.ToShortDateString(), editable: true));
        }
        fields.Add(new CardField("Card_Caption_SavingsPlan_ContractNumber", CardFieldKind.Text, text: dto?.ContractNumber ?? string.Empty, editable: true));
        fields.Add(new CardField("Card_Caption_SavingsPlan_Symbol", CardFieldKind.Symbol, symbolId: dto?.SymbolAttachmentId, editable: dto?.Id != Guid.Empty));

        var record = new CardRecord(fields, dto);
        return ApplyPendingValues(ApplyEnumTranslations(record));
    }

    // react to field changes so UI can update visibility/editability immediately
    public override void ValidateFieldValue(CardField field, object? newValue)
    {
        base.ValidateFieldValue(field, newValue);
        if (field == null) return;
        if (string.Equals(field.LabelKey, "Card_Caption_SavingsPlan_Type", StringComparison.OrdinalIgnoreCase) || string.Equals(field.LabelKey, "Card_Caption_SavingsPlan_Interval", StringComparison.OrdinalIgnoreCase))
        {
            _ = RebuildCardRecordAsync();
        }
    }

    public override void ValidateLookupField(CardField field, LookupItem? item)
    {
        base.ValidateLookupField(field, item);
        if (field == null) return;
        if (string.Equals(field.LabelKey, "Card_Caption_SavingsPlan_Type", StringComparison.OrdinalIgnoreCase) || string.Equals(field.LabelKey, "Card_Caption_SavingsPlan_Interval", StringComparison.OrdinalIgnoreCase))
        {
            _ = RebuildCardRecordAsync();
        }
    }

    private async Task RebuildCardRecordAsync()
    {
        try
        {
            CardRecord = await BuildCardRecordAsync(_loadedDto);
        }
        catch { }
        RaiseStateChanged();
    }

    // --- Ribbon provider ---
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var canSave = !string.IsNullOrWhiteSpace(Model.Name) && Model.Name.Trim().Length >= 2 && HasPendingChanges;

        // Navigation group
        var nav = new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, new List<UiRibbonAction>
        {
            new UiRibbonAction(
                Id: "Back",
                Label: localizer["Ribbon_Back"].Value,
                IconSvg: "<svg><use href='/icons/sprite.svg#back'/></svg>",
                Size: UiRibbonItemSize.Large,
                Disabled: false,
                Tooltip: null,
                Action: "Back",
                Callback: new Func<Task>(() => { RaiseUiActionRequested("Back"); return Task.CompletedTask; })
            )
        });

        // Manage group (Save, Archive, Delete)
        var manage = new UiRibbonTab(localizer["Ribbon_Group_Manage"].Value, new List<UiRibbonAction>
        {
            new UiRibbonAction(
                Id: "Save",
                Label: localizer["Ribbon_Save"].Value,
                IconSvg: "<svg><use href='/icons/sprite.svg#save'/></svg>",
                Size: UiRibbonItemSize.Large,
                Disabled: !canSave,
                Tooltip: null,
                Action: "Save",
                Callback: new Func<Task>(async () => { await SaveAsync(); })
            ),
            new UiRibbonAction(
                Id: "Archive",
                Label: localizer["Ribbon_Archive"].Value,
                IconSvg: "<svg><use href='/icons/sprite.svg#archive'/></svg>",
                Size: UiRibbonItemSize.Small,
                Disabled: !IsEdit,
                Tooltip: null,
                Action: "Archive",
                Callback: new Func<Task>(async () => { await ArchiveAsync(); })
            ),
            new UiRibbonAction(
                Id: "Delete",
                Label: localizer["Ribbon_Delete"].Value,
                IconSvg: "<svg><use href='/icons/sprite.svg#delete'/></svg>",
                Size: UiRibbonItemSize.Small,
                Disabled: !IsEdit,
                Tooltip: null,
                Action: "Delete",
                Callback: new Func<Task>(async () => { await DeleteAsync(); })
            )
        });

        // Linked information group
        var linked = new UiRibbonTab(localizer["Ribbon_Group_Linked"].Value, new List<UiRibbonAction>
        {
            new UiRibbonAction("Category", localizer["Ribbon_Category"].Value, "<svg><use href='/icons/sprite.svg#groups'/></svg>", UiRibbonItemSize.Small, Model.CategoryId is null, null, "OpenCategory", new Func<Task>(() => { var url = $"/card/savings-plans/categories/{Model.CategoryId}"; RaiseUiActionRequested("OpenCategory", url); return Task.CompletedTask; })),
            new UiRibbonAction("OpenPostings", localizer["Ribbon_Postings"].Value, "<svg><use href='/icons/sprite.svg#postings'/></svg>", UiRibbonItemSize.Small, !IsEdit, null, "OpenPostings", new Func<Task>(() => { var url = $"/list/postings/savings-plan/{Id}"; RaiseUiActionRequested("OpenPostings", url); return Task.CompletedTask; })),
            new UiRibbonAction("OpenAttachments", localizer["Ribbon_Attachments"].Value, "<svg><use href='/icons/sprite.svg#attachment'/></svg>", UiRibbonItemSize.Small, !IsEdit, null, "OpenAttachments", new Func<Task>(() => { RequestOpenAttachments(AttachmentEntityKind.SavingsPlan, Id); return Task.CompletedTask; }))
        });

        // Analysis group
        var analysis = new UiRibbonTab(localizer["Ribbon_Group_Analysis"].Value, new List<UiRibbonAction>
        {
            new UiRibbonAction(
                Id: "Recalculate",
                Label: localizer["Ribbon_Recalculate"].Value,
                IconSvg: "<svg><use href='/icons/sprite.svg#refresh'/></svg>",
                Size: UiRibbonItemSize.Small,
                Disabled: !IsEdit,
                Tooltip: null,
                Action: "Recalculate",
                Callback: new Func<Task>(async () => { await LoadAnalysisAsync(); })
            )
        });

        var tabs = new List<UiRibbonTab> { nav, manage, linked, analysis };
        var registers = new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
        var baseRegs = base.GetRibbonRegisterDefinition(localizer);
        if (baseRegs != null) registers.AddRange(baseRegs);
        return registers;
    }

    private async Task<bool> ArchiveAsync()
    {
        if (Id == Guid.Empty) return false;
        Loading = true; SetError(null, null); RaiseStateChanged();
        try
        {
            var ok = await ApiClient.SavingsPlans_ArchiveAsync(Id);
            if (!ok) { SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? "Archive failed"); return false; }
            // reload to reflect archived state
            await LoadAsync(Id);
            RaiseUiActionRequested("Archived");
            return true;
        }
        catch (Exception ex) { SetError(ApiClient.LastErrorCode ?? null, ApiClient.LastError ?? ex.Message); return false; }
        finally { Loading = false; RaiseStateChanged(); }
    }
    protected override bool IsSymbolUploadAllowed() => Id != Guid.Empty;
    // Implement BaseCardViewModel symbol parent and assignment hooks
    protected override (AttachmentEntityKind Kind, Guid ParentId) GetSymbolParent() => (AttachmentEntityKind.SavingsPlan, Id == Guid.Empty ? Guid.Empty : Id);

    protected override async Task AssignNewSymbolAsync(Guid? attachmentId)
    {
        // Ensure API call to set symbol on savings plan and reload
        try
        {
            if (attachmentId.HasValue)
            {
                await ApiClient.SavingsPlans_SetSymbolAsync(Id, attachmentId.Value);
            }
            else
            {
                await ApiClient.SavingsPlans_ClearSymbolAsync(Id);
            }
            await LoadAsync(Id);
        }
        catch
        {
            // swallow to keep behavior consistent; errors surfaced elsewhere
        }
    }

}

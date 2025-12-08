using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Statements;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Shared.Dtos.Securities;

namespace FinanceManager.Web.ViewModels.StatementDrafts;

public sealed class StatementDraftEntryDetailViewModel : ViewModelBase
{
    private readonly IServiceProvider _sp;

    public StatementDraftEntryDetailViewModel(IServiceProvider sp) : base(sp)
    {
        _sp = sp;
    }

    // Backing DTO
    private StatementDraftEntryDetailDto? _dto;

    public bool Loading { get; private set; }

    public Guid? DraftId => _dto?.DraftId;
    public string OriginalFileName => _dto?.OriginalFileName ?? string.Empty;
    public StatementDraftEntryDto Entry => _dto?.Entry ?? throw new InvalidOperationException();
    public Guid? PrevEntryId => _dto?.PrevEntryId;
    public Guid? NextEntryId => _dto?.NextEntryId;
    public Guid? NextOpenEntryId => _dto?.NextOpenEntryId;
    public decimal? SplitSum => _dto?.SplitSum;
    public decimal? Difference => _dto?.Difference;
    public Guid? BankContactId => _dto?.BankContactId;
    public bool ShowSecuritySection =>
        _dto != null && (
            _dto.Entry.SecurityId != null ||
            (BankContactId != null && _dto.Entry.ContactId != null && _dto.Entry.ContactId == BankContactId)
        );

    public bool IsDuplicate => _dto?.Entry.Status == StatementDraftEntryStatus.AlreadyBooked;
    public bool IsAnnounced => _dto?.Entry.Status == StatementDraftEntryStatus.Announced;
    public bool IsReadOnly => IsDuplicate || IsAnnounced;

    // UI state moved into VM
    public bool HasUnsavedChanges { get; private set; }
    public void MarkDirty() { HasUnsavedChanges = true; RaiseStateChanged(); }
    public void ClearDirty() { HasUnsavedChanges = false; RaiseStateChanged(); }

    public bool ShowBookWarnings { get; private set; }
    public StatementDraftBookingResultDto? LastBookingResult { get; private set; }
    public void ClearBookWarnings() { ShowBookWarnings = false; LastBookingResult = null; RaiseStateChanged(); }

    public DraftValidationResultDto? EntryValidation { get; private set; }

    public string? LastError { get; private set; }

    // Split dialog UI flag moved to VM so pages can bind directly
    public bool ShowSplitDialog { get; private set; }
    public void OpenSplitDialog() { ShowSplitDialog = true; RaiseStateChanged(); }
    public void CloseSplitDialog() { ShowSplitDialog = false; RaiseStateChanged(); }

    // Provide ribbon registers/tabs/actions via the central provider API
    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        // Safeguard: avoid accessing Entry when not loaded yet
        var hasSplitLink = _dto?.Entry?.SplitDraftId != null;

        var tabs = new List<UiRibbonTab>
        {
            // Navigation
            new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, new List<UiRibbonAction>
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
                ),
                new UiRibbonAction(
                    Id: "Prev",
                    Label: localizer["Ribbon_Prev"].Value,
                    IconSvg: "<svg><use href='/icons/sprite.svg#chevron-left'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: false,
                    Tooltip: null,
                    Action: "Prev",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("Prev"); return Task.CompletedTask; })
                ),
                new UiRibbonAction(
                    Id: "NextOpen",
                    Label: localizer["Ribbon_NextOpen"].Value,
                    IconSvg: "<svg><use href='/icons/sprite.svg#chevron-right'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: false,
                    Tooltip: null,
                    Action: "NextOpen",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("NextOpen"); return Task.CompletedTask; })
                )
            }),

            // Edit
            new UiRibbonTab(localizer["Ribbon_Group_Edit"].Value, new List<UiRibbonAction>
            {
                new UiRibbonAction(
                    Id: "Save",
                    Label: localizer["Ribbon_Save"].Value,
                    IconSvg: "<svg><use href='/icons/sprite.svg#save'/></svg>",
                    Size: UiRibbonItemSize.Large,
                    Disabled: false,
                    Tooltip: null,
                    Action: "Save",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("Save"); return Task.CompletedTask; })
                ),
                new UiRibbonAction(
                    Id: "DeleteEntry",
                    Label: localizer["Ribbon_DeleteEntry"].Value,
                    IconSvg: "<svg><use href='/icons/sprite.svg#delete'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: false,
                    Tooltip: null,
                    Action: "DeleteEntry",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("DeleteEntry"); return Task.CompletedTask; })
                ),
                new UiRibbonAction(
                    Id: "Reclassify",
                    Label: localizer["Ribbon_Reclassify"].Value,
                    IconSvg: "<svg><use href='/icons/sprite.svg#refresh'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: false,
                    Tooltip: null,
                    Action: "Reclassify",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("Reclassify"); return Task.CompletedTask; })
                ),
                new UiRibbonAction(
                    Id: "AssignSplit",
                    Label: localizer["Ribbon_AssignSplit"].Value,
                    IconSvg: "<svg><use href='/icons/sprite.svg#groups'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: false,
                    Tooltip: null,
                    Action: "AssignSplit",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("AssignSplit"); return Task.CompletedTask; })
                ),
                new UiRibbonAction(
                    Id: "ResetDuplicate",
                    Label: localizer["Ribbon_ResetDuplicate"].Value,
                    IconSvg: "<svg><use href='/icons/sprite.svg#refresh'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: !(IsDuplicate),
                    Tooltip: null,
                    Action: "ResetDuplicate",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("ResetDuplicate"); return Task.CompletedTask; })
                )
            }),

            // Book
            new UiRibbonTab(localizer["Ribbon_Group_Book"].Value, new List<UiRibbonAction>
            {
                new UiRibbonAction(
                    Id: "Validate",
                    Label: localizer["Ribbon_Validate"].Value,
                    IconSvg: "<svg><use href='/icons/sprite.svg#check'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: false,
                    Tooltip: null,
                    Action: "Validate",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("Validate"); return Task.CompletedTask; })
                ),
                new UiRibbonAction(
                    Id: "Book",
                    Label: localizer["Ribbon_Book"].Value,
                    IconSvg: "<svg><use href='/icons/sprite.svg#postings'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: false,
                    Tooltip: null,
                    Action: "Book",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("Book"); return Task.CompletedTask; })
                )
            }),

            // Related
            new UiRibbonTab(localizer["Ribbon_Group_Related"].Value, new List<UiRibbonAction>
            {
                new UiRibbonAction(
                    Id: "OpenAttachments",
                    Label: localizer["Ribbon_Attachments"].Value,
                    IconSvg: "<svg><use href='/icons/sprite.svg#attachment'/></svg>",
                    Size: UiRibbonItemSize.Large,
                    Disabled: false,
                    Tooltip: null,
                    Action: "OpenAttachments",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("OpenAttachments"); return Task.CompletedTask; })
                ),
                new UiRibbonAction(
                    Id: "OpenContact",
                    Label: localizer["Ribbon_OpenContact"].Value,
                    IconSvg: "<svg><use href='/icons/sprite.svg#account'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: false,
                    Tooltip: null,
                    Action: "OpenContact",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("OpenContact"); return Task.CompletedTask; })
                ),
                new UiRibbonAction(
                    Id: "OpenSavingsPlan",
                    Label: localizer["Ribbon_OpenSavingsPlan"].Value,
                    IconSvg: "<svg><use href='/icons/sprite.svg#groups'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: false,
                    Tooltip: null,
                    Action: "OpenSavingsPlan",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("OpenSavingsPlan"); return Task.CompletedTask; })
                ),
                new UiRibbonAction(
                    Id: "OpenSecurity",
                    Label: localizer["Ribbon_OpenSecurity"].Value,
                    IconSvg: "<svg><use href='/icons/sprite.svg#security'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: false,
                    Tooltip: null,
                    Action: "OpenSecurity",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("OpenSecurity"); return Task.CompletedTask; })
                ),
                new UiRibbonAction(
                    Id: "OpenSplitDraft",
                    Label: localizer["Ribbon_OpenSplitDraft"].Value,
                    IconSvg: "<svg><use href='/icons/sprite.svg#groups'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: !hasSplitLink,
                    Tooltip: null,
                    Action: "OpenSplitDraft",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("OpenSplitDraft"); return Task.CompletedTask; })
                ),
                new UiRibbonAction(
                    Id: "RemoveSplitLink",
                    Label: localizer["Ribbon_RemoveSplitLink"].Value,
                    IconSvg: "<svg><use href='/icons/sprite.svg#clear'/></svg>",
                    Size: UiRibbonItemSize.Small,
                    Disabled: !hasSplitLink,
                    Tooltip: null,
                    Action: "RemoveSplitLink",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("RemoveSplitLink"); return Task.CompletedTask; })
                )
            })
        };

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }

    public async Task LoadAsync(Guid draftId, Guid entryId)
    {
        Loading = true;
        RaiseStateChanged();
        try
        {
            var api = _sp.GetRequiredService<IApiClient>();
            var dto = await api.StatementDrafts_GetEntryAsync(draftId, entryId);
            _dto = dto;
            // clear transient UI state
            ShowBookWarnings = false;
            LastBookingResult = null;
            EntryValidation = null;
            LastError = null;
            HasUnsavedChanges = false;
        }
        finally
        {
            Loading = false;
            RaiseStateChanged();
        }
    }

    // Helper API methods for the page to call
    public async Task<IReadOnlyList<ContactDto>> LoadContactsAsync()
    {
        try
        {
            var api = _sp.GetRequiredService<IApiClient>();
            var list = await api.Contacts_ListAsync(skip: 0, take: 1000, type: null, all: true, nameFilter: null);
            _contactsCache = (list ?? Array.Empty<ContactDto>()).OrderBy(c => c.Name).ToList().ToList();
            return _contactsCache;
        }
        catch { return Array.Empty<ContactDto>(); }
    }

    public async Task<IReadOnlyList<SavingsPlanDto>> LoadUserSavingsPlansAsync()
    {
        try
        {
            var api = _sp.GetRequiredService<IApiClient>();
            var list = await api.SavingsPlans_ListAsync(true);
            _savingsPlansCache = (list ?? Array.Empty<SavingsPlanDto>()).ToList();
            return _savingsPlansCache;
        }
        catch { return Array.Empty<SavingsPlanDto>(); }
    }

    public async Task<IReadOnlyList<SecurityDto>> LoadSecuritiesIfNeededAsync()
    {
        try
        {
            var api = _sp.GetRequiredService<IApiClient>();
            var list = await api.Securities_ListAsync(onlyActive: true);
            _securitiesCache = (list ?? Array.Empty<SecurityDto>()).OrderBy(s => s.Name).ToList();
            return _securitiesCache;
        }
        catch { return Array.Empty<SecurityDto>(); }
    }

    public async Task<IReadOnlyList<StatementDraftDto>> LoadSplitCandidatesAsync()
    {
        try
        {
            var api = _sp.GetRequiredService<IApiClient>();
            var list = await api.StatementDrafts_ListOpenAsync(skip: 0, take: 500);
            // Filter: only drafts without bank contact assignment
            return (list ?? Array.Empty<StatementDraftDto>())
                .Where(d => d.DetectedAccountId == null)
                .ToList();
        }
        catch { return Array.Empty<StatementDraftDto>(); }
    }

    // Split dialog state + helpers
    public bool LoadingSplits { get; private set; }
    private List<StatementDraftDto> _candidateSplits = new();
    public IEnumerable<StatementDraftDto> CandidateSplits => _candidateSplits;
    public IEnumerable<StatementDraftDto> FilteredSplitDrafts => string.IsNullOrWhiteSpace(SplitFilter)
        ? _candidateSplits
        : _candidateSplits.Where(d => d.OriginalFileName.Contains(SplitFilter, StringComparison.OrdinalIgnoreCase));

    public string SplitFilter { get; set; } = string.Empty;
    public Guid? SelectedSplitDraftId { get; private set; }
    public string? SplitError { get; private set; }

    public async Task OpenSplitDialogAsync()
    {
        ShowSplitDialog = true;
        LoadingSplits = true;
        SplitError = null;
        SelectedSplitDraftId = null;
        RaiseStateChanged();
        try
        {
            var list = await LoadSplitCandidatesAsync();
            _candidateSplits = list.ToList();
        }
        catch { _candidateSplits = new(); }
        finally
        {
            LoadingSplits = false;
            RaiseStateChanged();
        }
    }

    public void SelectSplit(Guid? draftId)
    {
        SelectedSplitDraftId = draftId;
        RaiseStateChanged();
    }

    public async Task<bool> SetEntrySplitDraftAsync(Guid draftId, Guid entryId, Guid? splitDraftId)
    {
        try
        {
            var api = _sp.GetRequiredService<IApiClient>();
            var req = new FinanceManager.Shared.Dtos.Statements.StatementDraftSetSplitDraftRequest(splitDraftId);
            var dto = await api.StatementDrafts_SetEntrySplitDraftAsync(draftId, entryId, req);
            if (dto == null)
            {
                SplitError = api.LastError;
                LastError = api.LastError;
                RaiseStateChanged();
                return false;
            }
            // reload entry state
            await LoadAsync(draftId, entryId);
            return true;
        }
        catch (Exception ex)
        {
            SplitError = ex.Message;
            LastError = ex.Message;
            RaiseStateChanged();
            return false;
        }
    }

    public async Task<bool> SaveEntryAllAsync(Guid draftId, Guid entryId, StatementDraftSaveEntryAllRequest payload)
    {
        try
        {
            var api = _sp.GetRequiredService<IApiClient>();
            var resp = await api.StatementDrafts_SaveEntryAllAsync(draftId, entryId, payload);
            if (resp == null)
            {
                LastError = api.LastError;
                return false;
            }
            await LoadAsync(draftId, entryId);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Updates core fields of an entry (dates, amount, textual fields) via API and reloads entry state.
    /// </summary>
    public async Task<bool> UpdateEntryCoreAsync(Guid draftId, Guid entryId, DateTime bookingDate, DateTime? valutaDate, decimal amount, string subject, string? recipientName, string? currencyCode, string? bookingDescription)
    {
        try
        {
            var api = _sp.GetRequiredService<IApiClient>();
            var req = new StatementDraftUpdateEntryCoreRequest(bookingDate, valutaDate, amount, subject, recipientName, currencyCode, bookingDescription);
            var dto = await api.StatementDrafts_UpdateEntryCoreAsync(draftId, entryId, req);
            if (dto == null)
            {
                LastError = api.LastError;
                return false;
            }
            await LoadAsync(draftId, entryId);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    public async Task<bool> DeleteEntryAsync(Guid draftId, Guid entryId)
    {
        try
        {
            var api = _sp.GetRequiredService<IApiClient>();
            var ok = await api.StatementDrafts_DeleteEntryAsync(draftId, entryId);
            return ok;
        }
        catch { return false; }
    }

    public async Task<bool> ResetDuplicateEntryAsync(Guid draftId, Guid entryId)
    {
        try
        {
            var api = _sp.GetRequiredService<IApiClient>();
            var resp = await api.StatementDrafts_ResetDuplicateEntryAsync(draftId, entryId);
            if (resp != null)
            {
                await LoadAsync(draftId, entryId);
                return true;
            }
            LastError = api.LastError;
            return false;
        }
        catch (Exception ex)
        {
            LastError = ex.Message; return false;
        }
    }

    public async Task<bool> ReclassifyAsync(Guid draftId, Guid entryId)
    {
        try
        {
            var api = _sp.GetRequiredService<IApiClient>();
            var after = await api.StatementDrafts_ClassifyEntryAsync(draftId, entryId);
            if (after != null)
            {
                await LoadAsync(draftId, entryId);
                return true;
            }
            LastError = api.LastError;
            return false;
        }
        catch (Exception ex)
        {
            LastError = ex.Message; return false;
        }
    }

    public async Task<bool> ValidateEntryAsync(Guid draftId, Guid entryId)
    {
        try
        {
            var api = _sp.GetRequiredService<IApiClient>();
            EntryValidation = await api.StatementDrafts_ValidateEntryAsync(draftId, entryId);
            RaiseStateChanged();
            return true;
        }
        catch { EntryValidation = null; return false; }
    }

    public async Task<bool> BookEntryAsync(Guid draftId, Guid entryId, bool forceWarnings)
    {
        try
        {
            var api = _sp.GetRequiredService<IApiClient>();
            var res = await api.StatementDrafts_BookEntryAsync(draftId, entryId, forceWarnings);
            if (res == null)
            {
                LastError = api.LastError;
                return false;
            }
            // map to DTO used by UI
            LastBookingResult = new StatementDraftBookingResultDto(res.Success, res.HasWarnings, res.Validation, res.StatementImportId, res.TotalEntries, res.nextDraftId);
            ShowBookWarnings = res.HasWarnings;
            RaiseStateChanged();
            return res.Success;
        }
        catch (Exception ex)
        {
            LastError = ex.Message; return false;
        }
    }

    // Contact / SavingsPlan filter state and helpers
    private List<ContactDto> _contactsCache = new();
    // Securities cache for filtering/selection in the entry UI
    private List<SecurityDto> _securitiesCache = new();
    // Currently selected contact id for the entry
    public Guid? SelectedContactId { get; set; }
    public string ContactFilter
    {
        get => _contactFilter ?? string.Empty;
        set { _contactFilter = value ?? string.Empty; RaiseStateChanged(); }
    }
    private string? _contactFilter;
    public IEnumerable<ContactDto> FilteredContacts => string.IsNullOrWhiteSpace(ContactFilter)
        ? _contactsCache
        : _contactsCache.Where(c => c.Name.Contains(ContactFilter, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Assigns the selected contact to the draft entry via API and reloads entry state.
    /// </summary>
    public async Task<bool> SetEntryContactAsync(Guid draftId, Guid entryId, Guid? contactId)
    {
        try
        {
            var api = _sp.GetRequiredService<IApiClient>();
            var req = new FinanceManager.Shared.Dtos.Statements.StatementDraftSetContactRequest(contactId);
            var dto = await api.StatementDrafts_SetEntryContactAsync(draftId, entryId, req);
            if (dto == null)
            {
                LastError = api.LastError;
                return false;
            }
            // Refresh entry/details
            await LoadAsync(draftId, entryId);
            SelectedContactId = contactId;
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    private List<SavingsPlanDto> _savingsPlansCache = new();
    public string SavingsPlanFilter
    {
        get => _savingsPlanFilter ?? string.Empty;
        set { _savingsPlanFilter = value ?? string.Empty; RaiseStateChanged(); }
    }
    private string? _savingsPlanFilter;
    public IEnumerable<SavingsPlanDto> FilteredSavingsPlans => string.IsNullOrWhiteSpace(SavingsPlanFilter)
        ? _savingsPlansCache
        : _savingsPlansCache.Where(p => p.Name.Contains(SavingsPlanFilter, StringComparison.OrdinalIgnoreCase));

    // Securities filter/cache and helpers
    public Guid? SelectedSecurityId { get; set; }
    public string SecurityFilter
    {
        get => _securityFilter ?? string.Empty;
        set { _securityFilter = value ?? string.Empty; RaiseStateChanged(); }
    }
    private string? _securityFilter;
    public IEnumerable<SecurityDto> FilteredSecurities => string.IsNullOrWhiteSpace(SecurityFilter)
        ? _securitiesCache
        : _securitiesCache.Where(s => s.Name.Contains(SecurityFilter, StringComparison.OrdinalIgnoreCase) || s.Identifier.Contains(SecurityFilter, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Assign security details to the draft entry via API and reloads entry state.
    /// </summary>
    public async Task<bool> SetEntrySecurityAsync(Guid draftId, Guid entryId, Guid? securityId, SecurityTransactionType? transactionType, decimal? quantity, decimal? feeAmount, decimal? taxAmount)
    {
        try
        {
            var api = _sp.GetRequiredService<IApiClient>();
            var req = new FinanceManager.Shared.Dtos.Statements.StatementDraftSetEntrySecurityRequest(securityId, transactionType, quantity, feeAmount, taxAmount);
            var dto = await api.StatementDrafts_SetEntrySecurityAsync(draftId, entryId, req);
            if (dto == null)
            {
                LastError = api.LastError;
                RaiseStateChanged();
                return false;
            }
            // reload entry state
            await LoadAsync(draftId, entryId);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            RaiseStateChanged();
            return false;
        }
    }

    public async Task<bool> SetEntrySavingsPlanAsync(Guid draftId, Guid entryId, Guid? savingsPlanId)
    {
        try
        {
            var api = _sp.GetRequiredService<IApiClient>();
            var req = new FinanceManager.Shared.Dtos.Statements.StatementDraftSetSavingsPlanRequest(savingsPlanId);
            var dto = await api.StatementDrafts_SetEntrySavingsPlanAsync(draftId, entryId, req);
            if (dto == null)
            {
                LastError = api.LastError;
                RaiseStateChanged();
                return false;
            }
            // reload entry state
            await LoadAsync(draftId, entryId);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            RaiseStateChanged();
            return false;
        }
    }

    // Expression helpers moved here so pages can reuse evaluation logic
    public static bool TryEvalDecimal(string? expr, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(expr)) { return true; }
        try
        {
            var d = Eval(expr);
            value = (decimal)d;
            return true;
        }
        catch
        {
            value = null; return false;
        }
    }

    private static double Eval(string input)
    {
        var expr = new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray());
        expr = expr.Replace(',', '.');
        int i = 0;
        double ParseExpression()
        {
            double x = ParseTerm();
            while (i < expr.Length)
            {
                if (expr[i] == '+') { i++; x += ParseTerm(); }
                else if (expr[i] == '-') { i++; x -= ParseTerm(); }
                else break;
            }
            return x;
        }
        double ParseTerm()
        {
            double x = ParseFactor();
            while (i < expr.Length)
            {
                if (expr[i] == '*') { i++; x *= ParseFactor(); }
                else if (expr[i] == '/') { i++; x /= ParseFactor(); }
                else break;
            }
            return x;
        }
        double ParseFactor()
        {
            if (i >= expr.Length) throw new FormatException();
            if (expr[i] == '+') { i++; return ParseFactor(); }
            if (expr[i] == '-') { i++; return -ParseFactor(); }
            if (expr[i] == '(')
            {
                i++; var v = ParseExpression();
                if (i >= expr.Length || expr[i] != ')') throw new FormatException();
                i++; return v;
            }
            int start = i;
            while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == '.')) i++;
            if (start == i) throw new FormatException();
            var token = expr.Substring(start, i - start);
            if (!double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v2))
                throw new FormatException();
            return v2;
        }
        var result = ParseExpression();
        if (i != expr.Length) throw new FormatException();
        return result;
    }

    // UI edit mode controlled by VM so pages can bind directly
    private bool _editMode;
    public bool EditMode
    {
        get => _editMode;
        set
        {
            if (_editMode == value) return;
            _editMode = value;
            if (!_editMode)
            {
                // leaving edit mode: clear dirty state in VM
                ClearDirty();
            }
            RaiseStateChanged();
        }
    }

    // Add: load split draft candidates for current entry
    public async Task<IReadOnlyList<StatementDraftDto>> LoadSplitDraftCandidatesAsync(Guid draftId, Guid entryId)
    {
        // Delegate to existing method; parameters are kept for API parity with callers
        return await LoadSplitCandidatesAsync();
    }
}

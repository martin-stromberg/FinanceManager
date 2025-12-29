using System.Globalization;
using FinanceManager.Shared.Dtos.Statements;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.StatementDrafts;

[FinanceManager.Web.ViewModels.Common.CardRoute("statement-drafts")]
public sealed class StatementDraftCardViewModel : BaseCardViewModel<(string Key, string Value)>, IDeletableViewModel
{
    private readonly Shared.IApiClient _api;

    public StatementDraftCardViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<Shared.IApiClient>();
    }

    public Guid DraftId { get; private set; }
    public StatementDraftDetailDto? Draft { get; private set; }
    public DraftValidationResultDto? LastValidationResult { get; private set; }

    public override string Title => Draft?.OriginalFileName ?? base.Title;

    public override async Task LoadAsync(Guid id)
    {
        DraftId = id;
        Loading = true; SetError(null, null); RaiseStateChanged();
        try
        {
            if (id == Guid.Empty)
            {
                Draft = null;
                CardRecord = new CardRecord(new List<CardField>());
                return;
            }

            // Load full draft details so we can show counts and description
            Draft = await _api.StatementDrafts_GetAsync(id, headerOnly: false, ct: CancellationToken.None);
            if (Draft == null)
            {
                SetError(_api.LastErrorCode ?? null, _api.LastError ?? "Draft not found");
                CardRecord = new CardRecord(new List<CardField>());
                return;
            }

            // Build card record
            // Compute sum of entry amounts for display
            var sumAmounts = Draft.Entries?.Where(e => e.Status != StatementDraftEntryStatus.AlreadyBooked && e.Status != StatementDraftEntryStatus.Announced).Sum(e => e.Amount) ?? 0m;
            var fields = new List<CardField>
            {
                new CardField("Card_Caption_StatementDrafts_File", CardFieldKind.Text, text: Draft.OriginalFileName ?? string.Empty),
                new CardField("Card_Caption_StatementDrafts_Description", CardFieldKind.Text, text: Draft.Description ?? string.Empty),
                new CardField("Card_Caption_StatementDrafts_Status", CardFieldKind.Text, text: Draft.Status.ToString()),
                new CardField("Card_Caption_StatementDrafts_Entries", CardFieldKind.Text, text: $"{(Draft.Entries?.Count(e => e.Status != StatementDraftEntryStatus.Announced && e.Status != StatementDraftEntryStatus.AlreadyBooked) ?? 0)} ({(Draft.Entries?.Count ?? 0)})"),
                // Sum of all entry amounts
                new CardField("Card_Caption_StatementDrafts_SumAmounts", CardFieldKind.Currency, text: sumAmounts.ToString("C", CultureInfo.CurrentCulture), amount: sumAmounts)
            };

            // If this draft is assigned to an entry, show assigned amount and the difference
            if (Draft.ParentEntryId.HasValue || Draft.ParentEntryAmount.HasValue)
            {
                var assigned = Draft.ParentEntryAmount ?? 0m;
                var diff = assigned - sumAmounts;
                fields.Add(new CardField("Card_Caption_StatementDrafts_AssignedAmount", CardFieldKind.Currency, text: assigned.ToString("C", CultureInfo.CurrentCulture), amount: assigned));
                fields.Add(new CardField("Card_Caption_StatementDrafts_Difference", CardFieldKind.Currency, text: diff.ToString("C", CultureInfo.CurrentCulture), amount: diff));
            }

            CardRecord = new CardRecord(fields, Draft);

            // Expose entries via EmbeddedList which will page-load entries from API
            var entriesVm = new StatementDraftEntriesListViewModel(ServiceProvider, Draft.DraftId);
            EmbeddedList = entriesVm;
            await entriesVm.InitializeAsync();
        }
        catch (Exception ex)
        {
            SetError(null, ex.Message);
            CardRecord = new CardRecord(new List<CardField>());
        }
        finally
        {
            Loading = false; RaiseStateChanged();
        }
    }

    public override async Task<bool> DeleteAsync()
    {
        if (DraftId == Guid.Empty) return false;
        try
        {
            var ok = await _api.StatementDrafts_DeleteAsync(DraftId, CancellationToken.None);
            return ok;
        }
        catch
        {
            return false;
        }
    }

    // Symbol handling not supported for statement drafts
    protected override bool IsSymbolUploadAllowed() => false;
    protected override (Domain.Attachments.AttachmentEntityKind Kind, Guid ParentId) GetSymbolParent() => (Domain.Attachments.AttachmentEntityKind.StatementDraft, DraftId);
    protected override Task AssignNewSymbolAsync(Guid? attachmentId) => Task.CompletedTask;

    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var tabs = new List<UiRibbonTab>();

        // Navigation group
        var navItems = new List<UiRibbonAction>
        {
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Large, Draft == null, null, "Back", new Func<Task>(() => { RaiseUiActionRequested("Back"); return Task.CompletedTask; })),
            new UiRibbonAction("Prev", localizer["Ribbon_Prev"].Value, "<svg><use href='/icons/sprite.svg#chevron-left'/></svg>", UiRibbonItemSize.Small, Draft == null || Draft.PrevInUpload == null, null, "Prev", new Func<Task>(() => { RaiseUiActionRequested("Prev"); return Task.CompletedTask; })),
            new UiRibbonAction("Next", localizer["Ribbon_Next"].Value, "<svg><use href='/icons/sprite.svg#chevron-right'/></svg>", UiRibbonItemSize.Small, Draft == null || Draft.NextInUpload == null, null, "Next", new Func<Task>(() => { RaiseUiActionRequested("Next"); return Task.CompletedTask; }))
        };
        tabs.Add(new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, navItems));

        // Manage group
        var manageItems = new List<UiRibbonAction>
        {
            // Book now handled by this ViewModel to perform server-side booking and show warnings if present
            new UiRibbonAction("Book", localizer["Ribbon_Book"].Value, "<svg><use href='/icons/sprite.svg#postings'/></svg>", UiRibbonItemSize.Small, Draft == null, null, "Book", new Func<Task>(async () => { await BookAsync(); })),
            new UiRibbonAction("DeleteDraft", localizer["Ribbon_Delete"].Value, "<svg><use href='/icons/sprite.svg#delete'/></svg>", UiRibbonItemSize.Small, Draft == null, null, "DeleteDraft", new Func<Task>(() => { RaiseUiActionRequested("Delete"); return Task.CompletedTask; })),
            new UiRibbonAction("Classify", localizer["Ribbon_Reclassify"].Value, "<svg><use href='/icons/sprite.svg#refresh'/></svg>", UiRibbonItemSize.Small, Draft == null, null, "Classify", new Func<Task>(async () => { await ClassifyAsync(); })),
            new UiRibbonAction("Validate", localizer["Ribbon_Validate"].Value, "<svg><use href='/icons/sprite.svg#check'/></svg>", UiRibbonItemSize.Small, Draft == null, null, "Validate", new Func<Task>(async () => { await ValidateAsync(); }))
        };
        tabs.Add(new UiRibbonTab(localizer["Ribbon_Group_Manage"].Value, manageItems));

        // Statement group (Original view / download)
        var stmtItems = new List<UiRibbonAction>
        {
            new UiRibbonAction("ViewOriginal", localizer["Ribbon_ViewOriginal"].Value, "<svg><use href='/icons/sprite.svg#view'/></svg>", UiRibbonItemSize.Small, Draft == null, null, "ViewOriginal", new Func<Task>(() => { RaiseUiActionRequested("ViewOriginal"); return Task.CompletedTask; })),
            new UiRibbonAction("DownloadOriginal", localizer["Ribbon_Download"].Value, "<svg><use href='/icons/sprite.svg#download'/></svg>", UiRibbonItemSize.Small, Draft == null, null, "DownloadOriginal", new Func<Task>(() => { RaiseUiActionRequested("DownloadOriginal"); return Task.CompletedTask; }))
        };
        tabs.Add(new UiRibbonTab(localizer["Ribbon_Group_Statement"].Value, stmtItems));

        // Linked information group
        var linkedItems = new List<UiRibbonAction>
        {
            new UiRibbonAction("OpenAccountDetails", localizer["Ribbon_AccountDetails"].Value, "<svg><use href='/icons/sprite.svg#bank'/></svg>", UiRibbonItemSize.Small, Draft == null || Draft.DetectedAccountId == null, null, "OpenAccountDetails", new Func<Task>(() => { RaiseUiActionRequested("OpenAccountDetails"); return Task.CompletedTask; }))
        };
        tabs.Add(new UiRibbonTab(localizer["Ribbon_Group_Linked"].Value, linkedItems));

        var registers = new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
        var baseRegs = base.GetRibbonRegisterDefinition(localizer);
        if (baseRegs != null) registers.AddRange(baseRegs);
        return registers.Count == 0 ? null : registers;
    }

    public async Task ClassifyAsync()
    {
        if (DraftId == Guid.Empty) return;
        Loading = true; SetError(null, null); RaiseStateChanged();
        try
        {
            var updated = await _api.StatementDrafts_ClassifyAsync(DraftId, CancellationToken.None);
            if (updated == null)
            {
                SetError(_api.LastErrorCode ?? null, _api.LastError ?? "Classification failed");
                return;
            }

            // update draft and card fields
            Draft = updated;
            var fields = new List<CardField>
            {
                new CardField("Card_Caption_StatementDrafts_File", CardFieldKind.Text, text: Draft.OriginalFileName ?? string.Empty),
                new CardField("Card_Caption_StatementDrafts_Description", CardFieldKind.Text, text: Draft.Description ?? string.Empty),
                new CardField("Card_Caption_StatementDrafts_Status", CardFieldKind.Text, text: Draft.Status.ToString()),
                new CardField("Card_Caption_StatementDrafts_Entries", CardFieldKind.Text, text: (Draft.Entries?.Count ?? 0).ToString())
            };
            CardRecord = new CardRecord(fields, Draft);

            // Recreate embedded list so it picks up new symbol/name maps
            var entriesVm = new StatementDraftEntriesListViewModel(ServiceProvider, Draft.DraftId);
            EmbeddedList = entriesVm;
            await entriesVm.InitializeAsync();
        }
        catch (Exception ex)
        {
            SetError(null, ex.Message);
        }
        finally
        {
            Loading = false; RaiseStateChanged();
        }
    }

    public async Task ValidateAsync()
    {
        if (DraftId == Guid.Empty) return;
        Loading = true; SetError(null, null); RaiseStateChanged();
        try
        {
            var result = await _api.StatementDrafts_ValidateAsync(DraftId, CancellationToken.None);
            if (result == null)
            {
                SetError(_api.LastErrorCode ?? null, _api.LastError ?? "Validation failed");
                LastValidationResult = null;
                return;
            }

            // Store result on viewmodel so UI can render messages on the card page
            LastValidationResult = result;
            // propagate entry-specific messages to embedded list (if present)
            if (EmbeddedList is StatementDraftEntriesListViewModel entriesVm)
            {
                entriesVm.ApplyValidationMessages(result);
            }
            // Request embedded validation panel after the ribbon
            try
            {
                var parameters = new Dictionary<string, object?> { ["ValidationResult"] = result };
                var spec = new BaseViewModel.EmbeddedPanelSpec(typeof(FinanceManager.Web.Components.Shared.ValidationResultPanel), parameters, EmbeddedPanelPosition.AfterRibbon, true);
                RaiseUiEmbeddedPanelRequested(spec);
            }
            catch { }
            // notify UI
            RaiseStateChanged();
        }
        catch (Exception ex)
        {
            SetError(null, ex.Message);
        }
        finally
        {
            Loading = false; RaiseStateChanged();
        }
    }

    public async Task BookAsync()
    {
        if (DraftId == Guid.Empty) return;
        Loading = true; SetError(null, null); LastValidationResult = null; RaiseStateChanged();
        try
        {
            var res = await _api.StatementDrafts_BookAsync(DraftId, false, CancellationToken.None);
            if (res == null)
            {
                SetError(_api.LastErrorCode ?? null, _api.LastError ?? "Booking failed");
                return;
            }

            if (!res.Success)
            {
                // booking withheld due to warnings/errors -> show validation on card and propagate to embedded list
                LastValidationResult = res.Validation;
                if (EmbeddedList is StatementDraftEntriesListViewModel entriesVm)
                {
                    entriesVm.ApplyValidationMessages(res.Validation);
                }
                // Show validation panel after ribbon
                try
                {
                    var parameters = new Dictionary<string, object?> { ["ValidationResult"] = res.Validation };
                    var spec = new BaseViewModel.EmbeddedPanelSpec(typeof(FinanceManager.Web.Components.Shared.ValidationResultPanel), parameters, EmbeddedPanelPosition.AfterRibbon, true);
                    RaiseUiEmbeddedPanelRequested(spec);
                }
                catch { }
                // leave UI to display warnings; do not navigate
                return;
            }

            // success -> notify UI consumers (pages) to navigate back to overview
            // After booking the current draft, try to open the next draft in the upload group.
            // If no next draft exists, navigate back to the overview.
            try
            {
                var nextDraftId = Draft?.NextInUpload;
                if (nextDraftId.HasValue && nextDraftId.Value != Guid.Empty)
                {
                    // navigate to next draft card using Saved action so CardPage builds correct card URL
                    RaiseUiActionRequested("Saved", nextDraftId.Value.ToString());
                }
                else
                {
                    // no next draft -> return to overview
                    RaiseUiActionRequested("Back", null);
                }
            }
            catch
            {
                // fallback to overview on any error
                RaiseUiActionRequested("Back", null);
            }
        }
        catch (Exception ex)
        {
            SetError(null, ex.Message);
        }
        finally
        {
            Loading = false; RaiseStateChanged();
        }
    }
}

using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Statements;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace FinanceManager.Web.ViewModels.StatementDrafts;

public sealed class StatementDraftDetailViewModel : ViewModelBase
{
    private readonly IApiClient _api;

    public StatementDraftDetailViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<IApiClient>();
    }

    // Identity / state
    public Guid? DraftId { get; private set; }
    public bool Loading { get; private set; }

    // Data
    public StatementDraftDetailDto? Draft { get; private set; }

    // Expose sorted entries for UI binding
    public IReadOnlyList<StatementDraftEntryDto> SortedEntries =>
        Draft?.Entries == null
            ? Array.Empty<StatementDraftEntryDto>()
            : Draft.Entries
                .OrderBy(e => e.Status == StatementDraftEntryStatus.AlreadyBooked ? 2 : e.Status == StatementDraftEntryStatus.Announced ? 1: 0)
                .ThenBy(e => e.BookingDate)
                .ThenBy(e => e.BookingDescription)
                .ThenBy(e => e.RecipientName)
                .ToList();

    // UI state
    public bool ShowAttachments { get; private set; }

    public void ForDraft(Guid? id)
    {
        DraftId = id;
    }

    public void OpenAttachments()
    {
        ShowAttachments = true;
        RaiseStateChanged();
    }

    public void CloseAttachments()
    {
        ShowAttachments = false;
        RaiseStateChanged();
    }

    public override async ValueTask InitializeAsync(CancellationToken ct = default)
    {
        if (!IsAuthenticated)
        {
            RequireAuthentication(null);
            return;
        }

        await LoadAsync(ct);
        RaiseStateChanged();
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (Loading) return;
        Loading = true; RaiseStateChanged();
        try
        {
            if (DraftId == null) { Draft = null; return; }
            Draft = await _api.StatementDrafts_GetAsync(DraftId.Value, headerOnly: false, ct: ct);
        }
        catch { Draft = null; }
        finally { Loading = false; RaiseStateChanged(); }
    }

    public async Task<bool> DeleteAsync(CancellationToken ct = default)
    {
        if (DraftId == null) return false;
        try
        {
            var ok = await _api.StatementDrafts_DeleteAsync(DraftId.Value, ct);
            return ok;
        }
        catch { return false; }
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        var tabs = new List<UiRibbonTab>();

        // Navigation
        var navItems = new List<UiRibbonAction>
        {
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>", UiRibbonItemSize.Large, false, null, "Back", new Func<Task>(()=>{ RaiseUiActionRequested("Back"); return Task.CompletedTask; })),
            new UiRibbonAction("Prev", localizer["Ribbon_Prev"].Value, "<svg><use href='/icons/sprite.svg#chevron-left'/></svg>", UiRibbonItemSize.Small, Draft == null || Draft.PrevInUpload == null, null, "Prev", new Func<Task>(()=>{ RaiseUiActionRequested("Prev"); return Task.CompletedTask; })),
            new UiRibbonAction("Next", localizer["Ribbon_Next"].Value, "<svg><use href='/icons/sprite.svg#chevron-right'/></svg>", UiRibbonItemSize.Small, Draft == null || Draft.NextInUpload == null, null, "Next", new Func<Task>(()=>{ RaiseUiActionRequested("Next"); return Task.CompletedTask; }))
        };
        tabs.Add(new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, navItems));

        // Operations (server-side processing)
        var opsItems = new List<UiRibbonAction>
        {
            new UiRibbonAction("Classify", localizer["Ribbon_Reclassify"].Value, "<svg><use href='/icons/sprite.svg#refresh'/></svg>", UiRibbonItemSize.Small, DraftId == null, null, "Classify", new Func<Task>(()=>{ RaiseUiActionRequested("Classify"); return Task.CompletedTask; })),
            new UiRibbonAction("Validate", localizer["Ribbon_Validate"].Value, "<svg><use href='/icons/sprite.svg#check'/></svg>", UiRibbonItemSize.Small, DraftId == null, null, "Validate", new Func<Task>(()=>{ RaiseUiActionRequested("Validate"); return Task.CompletedTask; })),
            new UiRibbonAction("Book", localizer["Ribbon_Book"].Value, "<svg><use href='/icons/sprite.svg#postings'/></svg>", UiRibbonItemSize.Small, DraftId == null, null, "Book", new Func<Task>(()=>{ RaiseUiActionRequested("Book"); return Task.CompletedTask; })),
            new UiRibbonAction("DeleteDraft", localizer["Ribbon_Delete"].Value, "<svg><use href='/icons/sprite.svg#trash'/></svg>", UiRibbonItemSize.Small, DraftId == null, localizer["Ribbon_Tooltip_Delete"].Value, "DeleteDraft", new Func<Task>(()=>{ RaiseUiActionRequested("DeleteDraft"); return Task.CompletedTask; }))
        };
        tabs.Add(new UiRibbonTab(localizer["Ribbon_Group_Operations"].Value, opsItems));
        
        // Related (attachments & original file)
        var relatedItems = new List<UiRibbonAction>
        {
            new UiRibbonAction("OpenAttachments", localizer["Ribbon_Attachments"].Value, "<svg><use href='/icons/sprite.svg#attachment'/></svg>", UiRibbonItemSize.Small, DraftId == null, null, "OpenAttachments", new Func<Task>(()=>{ RaiseUiActionRequested("OpenAttachments"); return Task.CompletedTask; })),
            new UiRibbonAction("DownloadOriginal", localizer["Ribbon_Download"].Value, "<svg><use href='/icons/sprite.svg#download'/></svg>", UiRibbonItemSize.Small, DraftId == null, null, "DownloadOriginal", new Func<Task>(()=>{ RaiseUiActionRequested("DownloadOriginal"); return Task.CompletedTask; }))
        };
        if (Draft?.DetectedAccountId is Guid accId && accId != Guid.Empty)
        {
            relatedItems.Add(new UiRibbonAction(
                Id: "OpenAccountDetails",
                Label: localizer["Ribbon_AccountDetails"].Value,
                IconSvg: "<svg><use href='/icons/sprite.svg#bank'/></svg>",
                Size: UiRibbonItemSize.Small,
                Disabled: false,
                Tooltip: null,
                Action: "OpenAccountDetails",
                Callback: new Func<Task>(() => { RaiseUiActionRequested("OpenAccountDetails"); return Task.CompletedTask; })
            ));
        }
        tabs.Add(new UiRibbonTab(localizer["Ribbon_Group_Related"].Value, relatedItems));

        var registers = new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
        var baseRegs = base.GetRibbonRegisters(localizer);
        if (baseRegs != null) registers.AddRange(baseRegs);
        return registers.Count == 0 ? null : registers;
    }
}

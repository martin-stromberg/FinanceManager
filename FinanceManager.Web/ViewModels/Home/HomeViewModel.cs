using FinanceManager.Shared.Dtos.Statements;
using Microsoft.Extensions.Localization;
using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Web.ViewModels.Home;

public sealed class HomeViewModel : ViewModelBase
{
    private readonly FinanceManager.Shared.IApiClient _api;

    public HomeViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<FinanceManager.Shared.IApiClient>();
    }

    // Upload state
    public bool UploadInProgress { get; private set; }
    public int UploadTotal { get; private set; }
    public int UploadDone { get; private set; }
    public string? CurrentFileName { get; private set; }
    public bool ImportSuccess { get; private set; }
    public Guid? FirstDraftId { get; private set; }
    public ImportSplitInfoDto? SplitInfo { get; private set; }

    // KPI edit toggle
    public bool KpiEditMode { get; private set; }

    public int UploadPercent => UploadTotal == 0 ? 0 : (int)Math.Round((double)(UploadDone * 100m / UploadTotal));

    public void ToggleKpiEditMode()
    {
        KpiEditMode = !KpiEditMode;
        RaiseStateChanged();
    }

    public void StartUpload(int total)
    {
        UploadInProgress = true;
        UploadTotal = total;
        UploadDone = 0;
        CurrentFileName = null;
        ImportSuccess = false;
        FirstDraftId = null;
        SplitInfo = null;
        RaiseStateChanged();
    }

    public async Task UploadFileAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        CurrentFileName = fileName;
        RaiseStateChanged();
        try
        {
            var result = await _api.StatementDrafts_UploadAsync(stream, fileName, ct);
            if (result?.FirstDraft != null && FirstDraftId == null)
            {
                FirstDraftId = result.FirstDraft.DraftId;
            }
            if (result?.SplitInfo != null)
            {
                SplitInfo = result.SplitInfo;
            }
            if (FirstDraftId.HasValue)
            {
                ImportSuccess = true;
            }
        }
        catch
        {
            // ignore per-component behavior
        }
        finally
        {
            UploadDone++;
            if (UploadDone >= UploadTotal)
            {
                UploadInProgress = false;
                CurrentFileName = null;
            }
            RaiseStateChanged();
        }
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        // Build registers with tabs that act as groups in the UI
        var importAction = new UiRibbonAction(
            Id: "Import",
            Label: localizer["Ribbon_Import"],
            IconSvg: "<svg><use href='/icons/sprite.svg#upload'/></svg>",
            Size: UiRibbonItemSize.Large,
            Disabled: false,
            Tooltip: null,
            Action: "Import",
            Callback: new Func<Task>(() => { /* signal page to open import overlay */ RaiseUiActionRequested("Import"); return Task.CompletedTask; })
        );

        var kpiAction = new UiRibbonAction(
            Id: "ToggleKpi",
            Label: KpiEditMode ? localizer["Ribbon_Kpi_Done"] : localizer["Ribbon_Kpi_Edit"],
            IconSvg: KpiEditMode ? "<svg><use href='/icons/sprite.svg#check'/></svg>" : "<svg><use href='/icons/sprite.svg#edit'/></svg>",
            Size: UiRibbonItemSize.Large,
            Disabled: false,
            Tooltip: null,
            Action: "ToggleKpi",
            Callback: new Func<Task>(() => { ToggleKpiEditMode(); return Task.CompletedTask; })
        );

        var importTab = new UiRibbonTab(localizer["Ribbon_Group_Import"], new List<UiRibbonAction> { importAction });
        var kpiTab = new UiRibbonTab(localizer["Ribbon_Group_Kpi"], new List<UiRibbonAction> { kpiAction });

        var register = new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab> { importTab, kpiTab });

        return new List<UiRibbonRegister> { register };
    }
}

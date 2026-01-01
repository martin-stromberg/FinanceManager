using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Components.Forms;

namespace FinanceManager.Web.ViewModels.Home;

/// <summary>
/// View model for the home/dashboard page. Exposes import/upload state, KPI edit mode toggle
/// and provides ribbon actions for import and KPI editing.
/// </summary>
public sealed class HomeViewModel : ViewModelBase
{
    private readonly FinanceManager.Shared.IApiClient _api;

    /// <summary>
    /// Initializes a new instance of <see cref="HomeViewModel"/>.
    /// </summary>
    /// <param name="sp">Service provider used to resolve required services such as the API client.</param>
    public HomeViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<FinanceManager.Shared.IApiClient>();
    }

    // Upload state

    /// <summary>
    /// Gets a value indicating whether an import/upload operation is currently in progress.
    /// </summary>
    public bool UploadInProgress { get; private set; }

    /// <summary>
    /// Total number of files expected to be uploaded in the current batch.
    /// </summary>
    public int UploadTotal { get; private set; }

    /// <summary>
    /// Number of files already processed in the current upload batch.
    /// </summary>
    public int UploadDone { get; private set; }

    /// <summary>
    /// Name of the file currently being uploaded, or <c>null</c> when none.
    /// </summary>
    public string? CurrentFileName { get; private set; }

    /// <summary>
    /// Indicates whether the last import produced at least one successfully created draft.
    /// </summary>
    public bool ImportSuccess { get; private set; }

    /// <summary>
    /// When the import created new drafts, this contains the first draft id produced by the import; otherwise <c>null</c>.
    /// </summary>
    public Guid? FirstDraftId { get; private set; }

    /// <summary>
    /// Optional split information returned by the import service (when the uploaded file contained multiple segments).
    /// </summary>
    public ImportSplitInfoDto? SplitInfo { get; private set; }

    // KPI edit toggle

    /// <summary>
    /// When true the KPI edit mode is active in the UI allowing KPI adjustments.
    /// </summary>
    public bool KpiEditMode { get; private set; }

    /// <summary>
    /// Calculates the upload progress as a percentage (0-100). Returns 0 when <see cref="UploadTotal"/> is zero.
    /// </summary>
    public int UploadPercent => UploadTotal == 0 ? 0 : (int)Math.Round((double)(UploadDone * 100m / UploadTotal));

    /// <summary>
    /// Toggles the KPI edit mode state and raises a state change notification so the UI updates.
    /// </summary>
    public void ToggleKpiEditMode()
    {
        KpiEditMode = !KpiEditMode;
        RaiseStateChanged();
    }

    /// <summary>
    /// Begins a new upload batch and resets upload-related state.
    /// </summary>
    /// <param name="total">Number of files expected in the batch. Must be greater or equal to zero.</param>
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

    /// <summary>
    /// Uploads a single file stream to the import endpoint. This method updates upload progress state and
    /// will not throw on errors (component behavior intentionally swallows exceptions).
    /// </summary>
    /// <param name="stream">Stream containing the file content to upload.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="ct">Optional cancellation token used to cancel the upload.</param>
    /// <returns>A task that completes when the upload attempt has finished.</returns>
    /// <remarks>
    /// Errors from the API are intentionally caught and ignored to preserve the per-component behavior.
    /// The method still updates progress counters and ImportSuccess/FirstDraftId when the API response indicates success.
    /// </remarks>
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

    /// <summary>
    /// Builds ribbon register definitions for the Home view including import and KPI actions.
    /// </summary>
    /// <param name="localizer">Localizer used to resolve UI labels shown on the ribbon.</param>
    /// <returns>A list of <see cref="UiRibbonRegister"/> instances describing available actions.</returns>
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
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
            Callback: null
        )
        {
            FileCallback = async (InputFileChangeEventArgs e) =>
            {
                var files = e.GetMultipleFiles();
                if (files == null || files.Count == 0) return;
                StartUpload(files.Count);
                foreach (var file in files)
                {
                    await using var stream = file.OpenReadStream(10_000_000);
                    await UploadFileAsync(stream, file.Name);
                }
            }
        };

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
        var kpiTab = new UiRibbonTab(localizer["Ribbon_Group_KPI"], new List<UiRibbonAction> { kpiAction });

        var register = new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab> { importTab, kpiTab });

        return new List<UiRibbonRegister> { register };
    }
}

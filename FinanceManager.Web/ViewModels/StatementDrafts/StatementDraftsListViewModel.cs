using FinanceManager.Shared.Dtos.Statements;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Components;
using FinanceManager.Web.ViewModels;
using FinanceManager.Shared.Dtos.Admin;

namespace FinanceManager.Web.ViewModels.StatementDrafts;

public sealed class StatementDraftsListViewModel : BaseListViewModel<StatementDraftsListViewModel.DraftItem>, FinanceManager.Web.ViewModels.Common.IUploadFilesViewModel
{
    private readonly Shared.IApiClient _api;
    private readonly NavigationManager _nav;

    // Show classify and book-all background tasks on pages using this viewmodel
    public override BackgroundTaskType[]? VisibleBackgroundTaskTypes => new[] { BackgroundTaskType.ClassifyAllDrafts, BackgroundTaskType.BookAllDrafts };

    public StatementDraftsListViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<Shared.IApiClient>();
        _nav = sp.GetRequiredService<NavigationManager>();
        // disable generic range/search UI for drafts list
        AllowRangeFiltering = false;
        AllowSearchFiltering = false;
    }

    public sealed class DraftItem : IListItemNavigation
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public StatementDraftStatus Status { get; set; }
        public int PendingEntries { get; set; }

        public string GetNavigateUrl() => $"/card/statement-drafts/{Id}";
    }

    private int _skip;
    private const int PageSize = 3;

    protected override async Task LoadPageAsync(bool resetPaging)
    {
        if (!IsAuthenticated) return;
        if (resetPaging)
        {
            _skip = 0;
            Items.Clear();
        }

        try
        {
            var batch = await _api.StatementDrafts_ListOpenAsync(_skip, PageSize, CancellationToken.None);
            var list = batch?.ToList() ?? new List<FinanceManager.Shared.Dtos.Statements.StatementDraftDto>();
            if (list.Count < PageSize) CanLoadMore = false; else CanLoadMore = true;

            foreach (var d in list)
            {
                var pending = d.Entries.Count(e => e.Status != StatementDraftEntryStatus.AlreadyBooked);
                Items.Add(new DraftItem
                {
                    Id = d.DraftId,
                    FileName = d.OriginalFileName,
                    Description = d.Description,
                    Status = d.Status,
                    PendingEntries = pending
                });
            }
            _skip += list.Count;
        }
        catch
        {
            // swallow — UI shows empty state
            Items.Clear();
            CanLoadMore = false;
        }
    }

    protected override void BuildRecords()
    {
        var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();
        Columns = new List<ListColumn>
        {
            new ListColumn("", string.Empty, "2rem", ListColumnAlign.Left),
            new ListColumn("file", L["List_Th_StatementDrafts_File"].Value, "", ListColumnAlign.Left),
            new ListColumn("description", L["List_Th_StatementDrafts_Description"].Value, "", ListColumnAlign.Left),
            new ListColumn("status", L["List_Th_StatementDrafts_Status"].Value, "160px", ListColumnAlign.Left),
            new ListColumn("entries", L["List_Th_StatementDrafts_Entries"].Value, "110px", ListColumnAlign.Right)
        };

        Records = Items.Select(i => new ListRecord(new List<ListCell>
        {
            new ListCell(ListCellKind.Text, Text: string.Empty),
            new ListCell(ListCellKind.Text, Text: i.FileName),
            new ListCell(ListCellKind.Text, Text: i.Description ?? string.Empty),
            new ListCell(ListCellKind.Text, Text: i.Status.ToString()),
            new ListCell(ListCellKind.Text, Text: i.PendingEntries.ToString())
        }, i as object)).ToList();
    }

    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        var tabs = new List<UiRibbonTab>
        {
            new UiRibbonTab(localizer["Ribbon_Group_Manage"].Value, new List<UiRibbonAction>
            {
                new UiRibbonAction(
                    "DeleteAll",
                    localizer["Ribbon_DeleteAll"].Value,
                    "<svg><use href='/icons/sprite.svg#delete'/></svg>",
                    UiRibbonItemSize.Large,
                    false,
                    null,
                    "DeleteAll",
                    new Func<Task>(async () => { await DeleteAllAsync(); })),
                new UiRibbonAction(
                    "Reclassify",
                    localizer["Ribbon_Reclassify"].Value,
                    "<svg><use href='/icons/sprite.svg#refresh'/></svg>",
                    UiRibbonItemSize.Large,
                    false,
                    null,
                    "Reclassify",
                    new Func<Task>(async () => { await StartClassifyAsync(); })),
                new UiRibbonAction(
                    "MassBooking",
                    localizer["Ribbon_MassBooking"].Value,
                    "<svg><use href='/icons/sprite.svg#save'/></svg>",
                    UiRibbonItemSize.Large,
                    false,
                    null,
                    "MassBooking",
                    // Show options dialog before starting mass booking
                    new Func<Task>(async () => {
                        // Build overlay spec with callback to start booking
                        var overlayType = typeof(FinanceManager.Web.Components.Shared.MassBookingOptionsPanel);
                        var parameters = new Dictionary<string, object?>
                        {
                            ["StartCallback"] = new Func<bool, bool, bool, Task>(async (ignoreWarnings, abortOnFirstIssue, bookEntriesIndividually) =>
                            {
                                await StartBookAllAsync(ignoreWarnings, abortOnFirstIssue, bookEntriesIndividually);
                            }),
                            ["OverlayTitle"] = localizer["MassBooking_Title"].Value
                        };

                        var spec = new FinanceManager.Web.ViewModels.Common.BaseViewModel.UiOverlaySpec(overlayType, parameters);
                        RaiseUiActionRequested(null, payloadObject: spec);
                    })),
                new UiRibbonAction(
                    "Import",
                    localizer["Ribbon_Import"].Value,
                    "<svg><use href='/icons/sprite.svg#upload'/></svg>",
                    UiRibbonItemSize.Large,
                    false,
                    null,
                    "Import",
                    new Func<Task>(async () => { RaiseUiActionRequested("Import"); }))
            })
        };

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }

    // Upload state (for statement-drafts import)
    public bool UploadInProgress { get; private set; }
    public int UploadTotal { get; private set; }
    public int UploadDone { get; private set; }
    public string? CurrentFileName { get; private set; }
    public bool ImportSuccess { get; private set; }
    public Guid? FirstDraftId { get; private set; }
    public ImportSplitInfoDto? SplitInfo { get; private set; }

    public int UploadPercent => UploadTotal == 0 ? 0 : (int)Math.Round((double)(UploadDone * 100m / UploadTotal));

    // Classification background task state
    public bool IsClassifying { get; private set; }
    // Booking (mass) background task state
    public bool IsBooking { get; private set; }
    // Polling cancellation for book-all status
    private CancellationTokenSource? _bookPollingCts;

    private void StartUpload(int total)
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

    private async Task<StatementDraftUploadResult?> UploadFileAsync(Stream stream, string fileName, CancellationToken ct = default)
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
            return result;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
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

    async Task<FinanceManager.Web.ViewModels.Common.UploadResult?> FinanceManager.Web.ViewModels.Common.IUploadFilesViewModel.UploadFilesAsync(string payload, IEnumerable<(Stream Stream, string FileName)> files, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payload)) throw new ArgumentNullException(nameof(payload));
        // Only 'statementdraft' payload supported here. Future payloads can be routed accordingly.
        if (!string.Equals(payload, "statementdraft", StringComparison.OrdinalIgnoreCase))
            return null;

        var list = files.ToList();
        if (!list.Any()) return null;

        StartUpload(list.Count);
        int createdCount = 0;
        StatementDraftUploadResult? firstResult = null;
        foreach (var (stream, fileName) in list)
        {
            if (ct.IsCancellationRequested) break;
            var res = await UploadFileAsync(stream, fileName, ct).ConfigureAwait(false);
            if (res != null && res.FirstDraft != null)
            {
                createdCount++;
                if (firstResult == null) firstResult = res;
            }
        }

        return new FinanceManager.Web.ViewModels.Common.UploadResult { StatementDraftResult = firstResult, CreatedCount = createdCount };
    }

    /// <summary>
    /// Deletes all open statement drafts for the current user and resets list state.
    /// </summary>
    public async Task<bool> DeleteAllAsync()
    {
        try
        {
            var ok = await _api.StatementDrafts_DeleteAllAsync(CancellationToken.None);
            if (!ok)
            {
                SetError(_api.LastErrorCode ?? null, _api.LastError ?? "Delete failed");
                return false;
            }

            // Clear current items and reset paging so UI can reload
            Items.Clear();
            _skip = 0;
            CanLoadMore = true;
            RaiseStateChanged();
            return true;
        }
        catch (Exception ex)
        {
            SetError(null, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Starts a background classification task (classify all drafts) if not already running.
    /// </summary>
    public async Task StartClassifyAsync()
    {
        try
        {
            SetError(null, null);
            var status = await _api.StatementDrafts_StartClassifyAsync(CancellationToken.None);
            // If API returned an explicit status, honor it; otherwise assume queued/running
            IsClassifying = status?.running ?? true;
            RaiseStateChanged();
        }
        catch (Exception ex)
        {
            SetError(null, ex.Message);
            IsClassifying = false;
            RaiseStateChanged();
        }
    }

    /// <summary>
    /// Refreshes classification status; when completed reloads the list.
    /// </summary>
    public async Task RefreshClassifyStatusAsync()
    {
        try
        {
            SetError(null, null);
            var status = await _api.StatementDrafts_GetClassifyStatusAsync(CancellationToken.None);
            IsClassifying = status?.running ?? false;
            // if classification finished, refresh list
            if (!IsClassifying)
            {
                await InitializeAsync();
            }
            RaiseStateChanged();
        }
        catch (Exception ex)
        {
            SetError(null, ex.Message);
            IsClassifying = false;
            RaiseStateChanged();
        }
    }

    /// <summary>
    /// Starts mass booking (book-all) background task.
    /// </summary>
    public async Task StartBookAllAsync(bool ignoreWarnings, bool abortOnFirstIssue, bool bookEntriesIndividually)
    {
        try
        {
            SetError(null, null);
            var status = await _api.StatementDrafts_StartBookAllAsync(ignoreWarnings, abortOnFirstIssue, bookEntriesIndividually, CancellationToken.None);
            IsBooking = status?.Running ?? true;
            RaiseStateChanged();

            // If booking is running on server, start polling status every second until finished
            if (IsBooking)
            {
                StartBookStatusPolling();
            }
            else
            {
                // If booking already finished synchronously, reload list
                await InitializeAsync();
            }
        }
        catch (Exception ex)
        {
            SetError(null, ex.Message);
            IsBooking = false;
            RaiseStateChanged();
        }
    }

    /// <summary>
    /// Refreshes book-all status; when completed reloads the list.
    /// </summary>
    public async Task RefreshBookStatusAsync()
    {
        try
        {
            SetError(null, null);
            var status = await _api.StatementDrafts_GetBookAllStatusAsync(CancellationToken.None);
            IsBooking = status?.Running ?? false;
            if (!IsBooking)
            {
                // Stop polling if active and refresh list
                StopBookStatusPolling();
                await InitializeAsync();
            }
            RaiseStateChanged();
        }
        catch (Exception ex)
        {
            SetError(null, ex.Message);
            IsBooking = false;
            RaiseStateChanged();
        }
    }

    private void StartBookStatusPolling()
    {
        // cancel any existing polling
        StopBookStatusPolling();
        _bookPollingCts = new CancellationTokenSource();
        var token = _bookPollingCts.Token;
        // fire-and-forget polling loop; catch exceptions to avoid unobserved exceptions
        _ = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested && IsBooking)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), token);
                    }
                    catch (TaskCanceledException) { break; }

                    if (token.IsCancellationRequested) break;
                    try
                    {
                        await RefreshBookStatusAsync();
                    }
                    catch
                    {
                        // swallow per-refresh exceptions; RefreshBookStatusAsync already sets state and errors
                    }
                }
            }
            finally
            {
                // ensure polling CTS is disposed when loop exits
                StopBookStatusPolling();
            }
        }, CancellationToken.None);
    }

    private void StopBookStatusPolling()
    {
        try
        {
            if (_bookPollingCts != null)
            {
                if (!_bookPollingCts.IsCancellationRequested) _bookPollingCts.Cancel();
                _bookPollingCts.Dispose();
                _bookPollingCts = null;
            }
        }
        catch { }
    }

    /// <summary>
    /// Attempts to cancel running book-all background task.
    /// </summary>
    public async Task CancelBookingAsync()
    {
        try
        {
            SetError(null, null);
            // Stop local polling first to avoid races
            StopBookStatusPolling();
            await _api.StatementDrafts_CancelBookAllAsync(CancellationToken.None);
            // after cancel, refresh status (this will also reload list when finished)
            await RefreshBookStatusAsync();
        }
        catch (Exception ex)
        {
            SetError(null, ex.Message);
            RaiseStateChanged();
        }
    }
}

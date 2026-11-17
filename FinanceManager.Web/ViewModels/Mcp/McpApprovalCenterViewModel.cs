using FinanceManager.Domain.Attachments;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.Services;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Mcp;

/// <summary>
/// Approval-center view model (<c>/card/mcp-approvals</c>, ui-konzept.md §4.6, F-1001):
/// inbox of pending <c>AiActionRequest</c>s plus a switchable history of decided ones,
/// filter by status/access/tool, server paging, detail drilling with the server-side
/// effect preview and the decision actions approve/reject/cancel. Approve runs through
/// <see cref="IConfirmationService"/> — critical severity on destructive effects — and
/// reject requires a comment (played back to the client as decision feedback). Batch
/// approval of selected pending requests uses the batch endpoint; every request keeps
/// its own state transition (approval-konzept.md §7.4).
/// </summary>
[CardRoute("mcp-approvals")]
public sealed class McpApprovalCenterViewModel : BaseCardViewModel<(string Key, string Value)>
{
    /// <summary>Page size of the inbox/history loading.</summary>
    private const int PageSize = 50;

    private readonly IApiClient _api;

    /// <summary>Initializes a new instance of <see cref="McpApprovalCenterViewModel"/>.</summary>
    public McpApprovalCenterViewModel(IServiceProvider sp, IApiClient apiClient) : base(sp)
    {
        _api = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    /// <summary>Title shown on the card header.</summary>
    public override string Title => Localizer?["McpApproval_Title"].Value ?? "Approvals";

    /// <summary>Loaded request rows (newest first).</summary>
    public List<AiActionRequestListItemDto> Items { get; } = new();

    /// <summary>Whether more pages are available.</summary>
    public bool CanLoadMore { get; private set; }

    /// <summary>Own accesses used for the access filter dropdown.</summary>
    public IReadOnlyList<McpAccessListItemDto> Accesses { get; private set; } = Array.Empty<McpAccessListItemDto>();

    /// <summary>Currently expanded request id, or <see langword="null"/>.</summary>
    public Guid? ExpandedId { get; private set; }

    /// <summary>Detail of the expanded request, or <see langword="null"/>.</summary>
    public AiActionRequestDetailDto? Detail { get; private set; }

    /// <summary>Indicates <c>Err_Mcp_Disabled</c> — the view then shows the disabled hint.</summary>
    public bool IsMcpDisabled { get; private set; }

    /// <summary>Whether the history view (decided requests) is shown instead of the inbox.</summary>
    public bool ShowHistory { get; private set; }

    /// <summary>Pending status filter (AiActionRequestStatus as short, null = inbox=Pending).</summary>
    public short? FilterStatus { get; private set; }

    /// <summary>Pending access filter.</summary>
    public Guid? FilterAccessId { get; private set; }

    /// <summary>Pending tool filter (exact <c>fm_*</c> name, applied server-side).</summary>
    public string? FilterToolName { get; private set; }

    /// <summary>Ids of pending requests selected for the batch decision.</summary>
    public HashSet<Guid> SelectedIds { get; } = new();

    /// <summary>Batch outcome of the last multi decision (per-item results), or <see langword="null"/>.</summary>
    public IReadOnlyList<AiActionRequestBatchItemDto>? LastBatchResult { get; private set; }

    /// <summary>Whether a decision action is currently running.</summary>
    public bool Busy { get; private set; }

    /// <summary>Reject comment entered in the detail view (required for reject).</summary>
    public string? DecisionComment { get; private set; }

    /// <summary>Sets the decision comment (does not reload).</summary>
    public void SetDecisionComment(string? comment)
    {
        DecisionComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        RaiseStateChanged();
    }

    /// <summary>Sets the status filter (does not reload).</summary>
    public void SetFilterStatus(short? status) { FilterStatus = status; RaiseStateChanged(); }

    /// <summary>Sets the access filter (does not reload).</summary>
    public void SetFilterAccess(Guid? accessId) { FilterAccessId = accessId; RaiseStateChanged(); }

    /// <summary>Sets the tool filter (does not reload).</summary>
    public void SetFilterTool(string? toolName) { FilterToolName = string.IsNullOrWhiteSpace(toolName) ? null : toolName.Trim(); RaiseStateChanged(); }

    /// <summary>Switches between inbox (pending only) and history (decided) and reloads.</summary>
    public async Task SetHistoryAsync(bool history)
    {
        ShowHistory = history;
        SelectedIds.Clear();
        await LoadAsync(Guid.Empty);
    }

    /// <summary>Applies the pending filters and reloads.</summary>
    public Task ApplyFiltersAsync() => LoadAsync(Guid.Empty);

    /// <summary>Clears all filters and reloads.</summary>
    public async Task ClearFiltersAsync()
    {
        FilterStatus = null; FilterAccessId = null; FilterToolName = null;
        await LoadAsync(Guid.Empty);
    }

    /// <inheritdoc />
    public override async Task InitializeAsync(Guid id)
    {
        await LoadAsync(id);
    }

    /// <summary>Loads the access dropdown and the first page of the current view.</summary>
    public override async Task LoadAsync(Guid id)
    {
        Loading = true; SetError(null, null); IsMcpDisabled = false; RaiseStateChanged();
        try
        {
            try
            {
                var status = await _api.Mcp_GetStatusAsync(CancellationToken.None);
                IsMcpDisabled = !status.Enabled;
            }
            catch { /* status is best-effort; the request load reports real failures */ }
            if (IsMcpDisabled)
            {
                Accesses = Array.Empty<McpAccessListItemDto>();
                Items.Clear(); CanLoadMore = false;
                return;
            }

            try { Accesses = await _api.Mcp_ListAccessesAsync(take: 200); }
            catch { Accesses = Array.Empty<McpAccessListItemDto>(); }

            Items.Clear();
            ExpandedId = null; Detail = null;
            var page = await LoadPageAsync(0);
            Items.AddRange(page);
            CanLoadMore = page.Count >= PageSize;
        }
        catch (Exception ex)
        {
            CanLoadMore = false;
            if (string.Equals(_api.LastErrorCode, "Err_Mcp_Disabled", StringComparison.Ordinal))
            {
                IsMcpDisabled = true;
            }
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
        }
        finally { Loading = false; RaiseStateChanged(); }
    }

    /// <summary>Loads the next page and appends it.</summary>
    public async Task LoadMoreAsync()
    {
        if (!CanLoadMore) return;
        Loading = true; RaiseStateChanged();
        try
        {
            var page = await LoadPageAsync(Items.Count);
            Items.AddRange(page);
            CanLoadMore = page.Count >= PageSize;
        }
        catch (Exception ex)
        {
            CanLoadMore = false;
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
        }
        finally { Loading = false; RaiseStateChanged(); }
    }

    /// <summary>
    /// Effective server-side status filter: the inbox always queries Pending; the history
    /// uses the chosen decided status (or all decided states when unset).
    /// </summary>
    private async Task<IReadOnlyList<AiActionRequestListItemDto>> LoadPageAsync(int skip)
    {
        // Status, access and tool filters are server-side (ui-konzept.md §6.2); only the
        // "all decided states" union of the history view is folded client-side.
        if (ShowHistory)
        {
            if (FilterStatus.HasValue && FilterStatus.Value != (short)AiActionRequestStatus.Pending)
            {
                return await _api.Mcp_ListActionRequestsAsync(FilterStatus, FilterAccessId, FilterToolName, skip, PageSize);
            }
            // No single status filter for "decided" — fetch and filter client-side.
            var all = await _api.Mcp_ListActionRequestsAsync(null, FilterAccessId, FilterToolName, skip, PageSize);
            return all.Where(r => r.Status != (short)AiActionRequestStatus.Pending).ToList();
        }
        return await _api.Mcp_ListActionRequestsAsync(
            FilterStatus ?? (short)AiActionRequestStatus.Pending, FilterAccessId, FilterToolName, skip, PageSize);
    }

    /// <summary>Expands a request (loads its detail incl. effect preview) or collapses it.</summary>
    public async Task ToggleDetailsAsync(Guid requestId)
    {
        if (ExpandedId == requestId)
        {
            ExpandedId = null; Detail = null; DecisionComment = null; RaiseStateChanged();
            return;
        }
        ExpandedId = requestId; Detail = null; DecisionComment = null; RaiseStateChanged();
        try
        {
            Detail = await _api.Mcp_GetActionRequestAsync(requestId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
        }
        RaiseStateChanged();
    }

    /// <summary>Toggles the batch-selection mark of a pending request.</summary>
    public void ToggleSelected(Guid requestId)
    {
        if (!SelectedIds.Remove(requestId))
        {
            SelectedIds.Add(requestId);
        }
        RaiseStateChanged();
    }

    /// <summary>
    /// Whether the row describes a destructive/final effect (critical confirmation):
    /// classified from tool name + action key (mirrors the server-side preview rules).
    /// </summary>
    public static bool IsDestructive(string toolName, string actionKey)
        => toolName switch
        {
            "fm_statement_draft_book" or "fm_statement_draft_discard" or "fm_posting_reverse" => true,
            _ => actionKey is "merge" or "delete" or "archive"
        };

    /// <summary>Whether the expanded detail describes a destructive/final effect.</summary>
    public bool DetailDestructive
        => Detail?.EffectPreview?.IsDestructive
            ?? (Detail is not null && IsDestructive(Detail.ToolName, Detail.ActionKey));

    /// <summary>
    /// Approves the expanded pending request after a confirmation — critical severity on
    /// destructive/final effects (F-1001, approval-konzept.md §7.3).
    /// </summary>
    public async Task<bool> ApproveExpandedAsync()
    {
        if (Detail is null || Busy) return false;
        var destructive = DetailDestructive;
        var confirmed = await ConfirmationService.ConfirmAsync(new(
            TitleResourceKey: destructive ? "Confirmation_McpApproveDestructive_Title" : "Confirmation_McpApprove_Title",
            MessageResourceKey: destructive ? "Confirmation_McpApproveDestructive_Message" : "Confirmation_McpApprove_Message",
            Severity: destructive ? ConfirmationSeverity.Critical : ConfirmationSeverity.Warning), CancellationToken.None);
        if (!confirmed) return false;
        return await ApproveAsync(Detail.Id, DecisionComment);
    }

    /// <summary>
    /// Rejects the expanded pending request — the comment is mandatory (decision feedback
    /// played back to the client, F-1001).
    /// </summary>
    public async Task<bool> RejectExpandedAsync()
    {
        if (Detail is null || Busy || string.IsNullOrWhiteSpace(DecisionComment)) return false;
        return await RejectAsync(Detail.Id, DecisionComment);
    }

    /// <summary>Withdraws the expanded pending request (owner-side cancel).</summary>
    public async Task<bool> CancelExpandedAsync()
    {
        if (Detail is null || Busy) return false;
        Busy = true; RaiseStateChanged();
        try
        {
            var ok = await _api.Mcp_CancelRequestAsync(Detail.Id, CancellationToken.None);
            if (!ok)
            {
                SetError("Err_NotFound", null);
                return false;
            }
            await AfterDecisionAsync();
            return true;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
            return false;
        }
        finally { Busy = false; RaiseStateChanged(); }
    }

    /// <summary>
    /// Approves all selected pending requests via the batch endpoint — one confirmation,
    /// one own state transition per request (approval-konzept.md §7.4).
    /// </summary>
    public async Task ApproveSelectedAsync()
    {
        if (Busy || SelectedIds.Count == 0) return;
        var destructive = Items.Any(i => SelectedIds.Contains(i.Id) && IsDestructive(i.ToolName, i.ActionKey));
        var confirmed = await ConfirmationService.ConfirmAsync(new(
            TitleResourceKey: "Confirmation_McpBatchApprove_Title",
            MessageResourceKey: "Confirmation_McpBatchApprove_Message",
            Severity: destructive ? ConfirmationSeverity.Critical : ConfirmationSeverity.Warning), CancellationToken.None);
        if (!confirmed) return;
        await DecideBatchAsync(approve: true);
    }

    /// <summary>Rejects all selected pending requests via the batch endpoint.</summary>
    public async Task RejectSelectedAsync(string? comment)
    {
        if (Busy || SelectedIds.Count == 0) return;
        await DecideBatchAsync(approve: false, comment);
    }

    private async Task DecideBatchAsync(bool approve, string? comment = null)
    {
        Busy = true; SetError(null, null); RaiseStateChanged();
        try
        {
            LastBatchResult = await _api.Mcp_DecideActionRequestsBatchAsync(
                SelectedIds.ToList(), approve, comment ?? DecisionComment, CancellationToken.None);
            SelectedIds.Clear();
            await AfterDecisionAsync();
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
        }
        finally { Busy = false; RaiseStateChanged(); }
    }

    private async Task<bool> ApproveAsync(Guid id, string? comment)
    {
        Busy = true; SetError(null, null); RaiseStateChanged();
        try
        {
            var ok = await _api.Mcp_ApproveRequestAsync(id, comment, CancellationToken.None);
            if (!ok) { SetError("Err_NotFound", null); return false; }
            await AfterDecisionAsync();
            return true;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
            return false;
        }
        finally { Busy = false; RaiseStateChanged(); }
    }

    private async Task<bool> RejectAsync(Guid id, string? comment)
    {
        Busy = true; SetError(null, null); RaiseStateChanged();
        try
        {
            var ok = await _api.Mcp_RejectRequestAsync(id, comment, CancellationToken.None);
            if (!ok) { SetError("Err_NotFound", null); return false; }
            await AfterDecisionAsync();
            return true;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
            return false;
        }
        finally { Busy = false; RaiseStateChanged(); }
    }

    private async Task AfterDecisionAsync()
    {
        ExpandedId = null; Detail = null; DecisionComment = null;
        await LoadAsync(Guid.Empty);
    }

    /// <summary>Requests the approval-center panel rendered below the card area.</summary>
    public override IReadOnlyList<EmbeddedPanelSpec> GetEmbeddedPanelSpecs(EmbeddedPanelPosition position)
    {
        if (position != EmbeddedPanelPosition.AfterCard)
        {
            return Array.Empty<EmbeddedPanelSpec>();
        }
        var parms = new Dictionary<string, object?> { ["Provider"] = this };
        return new[]
        {
            new EmbeddedPanelSpec(typeof(McpApprovalsPanel), parms, EmbeddedPanelPosition.AfterCard, Visible: true)
        };
    }

    /// <summary>Ribbon: Back (to the setup register) and Reload.</summary>
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var navigation = new List<UiRibbonAction>
        {
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>",
                UiRibbonItemSize.Large, false, null,
                () => { RaiseUiActionRequested("Back", "/card/setup"); return Task.CompletedTask; })
            { MobileShortcut = true }
        };
        var actions = new List<UiRibbonAction>
        {
            new UiRibbonAction("Reload", localizer["Ribbon_Reload"].Value, "<svg><use href='/icons/sprite.svg#refresh'/></svg>",
                UiRibbonItemSize.Small, false, null, async () => { await LoadAsync(Guid.Empty); })
        };
        var tabs = new List<UiRibbonTab>
        {
            new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, navigation),
            new UiRibbonTab(localizer["Ribbon_Group_Manage"].Value, actions)
        };
        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }

    /// <inheritdoc />
    protected override bool IsSymbolUploadAllowed() => false;

    /// <inheritdoc />
    protected override SymbolParentRef GetSymbolParent()
        => new(AttachmentEntityKind.None, Guid.Empty);

    /// <inheritdoc />
    protected override Task AssignNewSymbolAsync(Guid? attachmentId)
        => Task.CompletedTask;
}

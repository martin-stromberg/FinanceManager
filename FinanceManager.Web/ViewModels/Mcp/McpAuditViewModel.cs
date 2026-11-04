using FinanceManager.Domain.Attachments;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Mcp;

/// <summary>
/// Audit timeline view model (<c>/card/mcp-audit</c>, ui-konzept.md §4.5, F-0602). Loads the
/// own MCP audit entries through <see cref="IApiClient"/> with AND-combined filters
/// (access, category, result, tool, time range, correlation id), supports server paging,
/// drilling (entry → detail incl. field deltas + payload + correlation id), the hash-chain
/// verification ("Kette prüfen") and export URL building (csv/json, downloaded via the
/// cookie-authenticated export endpoint). A deep link <c>?accessId={guid}</c> preselects the
/// access filter (Verlauf-Tab der Zugangskarte).
/// </summary>
[CardRoute("mcp-audit")]
public sealed class McpAuditViewModel : BaseCardViewModel<(string Key, string Value)>
{
    /// <summary>Page size of the timeline loading.</summary>
    private const int PageSize = 50;

    private readonly IApiClient _api;

    /// <summary>
    /// Initializes a new instance of <see cref="McpAuditViewModel"/>.
    /// </summary>
    /// <param name="sp">Service provider used to resolve additional services.</param>
    /// <param name="apiClient">API client used to call the audit endpoints.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="apiClient"/> is <c>null</c>.</exception>
    public McpAuditViewModel(IServiceProvider sp, IApiClient apiClient) : base(sp)
    {
        _api = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    /// <summary>Title shown on the card header.</summary>
    public override string Title => Localizer?["McpAudit_Title"].Value ?? "Audit log";

    /// <summary>Loaded timeline items (newest first).</summary>
    public List<McpAuditEntryListItemDto> Entries { get; } = new();

    /// <summary>Whether more timeline pages are available.</summary>
    public bool CanLoadMore { get; private set; }

    /// <summary>Own accesses used for the access filter dropdown.</summary>
    public IReadOnlyList<McpAccessListItemDto> Accesses { get; private set; } = Array.Empty<McpAccessListItemDto>();

    /// <summary>Currently expanded entry id, or <see langword="null"/>.</summary>
    public Guid? ExpandedId { get; private set; }

    /// <summary>Detail of the expanded entry, or <see langword="null"/>.</summary>
    public McpAuditEntryDetailDto? Detail { get; private set; }

    /// <summary>Result of the last integrity check, or <see langword="null"/>.</summary>
    public McpAuditVerifyResultDto? Verification { get; private set; }

    /// <summary>Whether the integrity check is running.</summary>
    public bool Verifying { get; private set; }

    /// <summary>
    /// Indicates whether the API reported <c>Err_Mcp_Disabled</c>; the view then shows a
    /// friendly hint instead of entries.
    /// </summary>
    public bool IsMcpDisabled { get; private set; }

    // --- Filter state (applied via ApplyFiltersAsync) ---

    /// <summary>Pending access filter (id of the selected access, or null for all).</summary>
    public Guid? FilterAccessId { get; private set; }

    /// <summary>Pending category filter (McpAuditCategory as short, or null for all).</summary>
    public short? FilterCategory { get; private set; }

    /// <summary>Pending result filter (exact result code, or null for all).</summary>
    public string? FilterResult { get; private set; }

    /// <summary>Pending tool filter (exact <c>fm_*</c> tool name).</summary>
    public string? FilterToolName { get; private set; }

    /// <summary>Pending correlation-id filter.</summary>
    public Guid? FilterCorrelationId { get; private set; }

    /// <summary>Pending inclusive lower bound (UTC).</summary>
    public DateTime? FilterFrom { get; private set; }

    /// <summary>Pending inclusive upper bound (UTC).</summary>
    public DateTime? FilterTo { get; private set; }

    /// <summary>Sets the access filter and marks the state changed (does not reload).</summary>
    public void SetFilterAccess(Guid? accessId) { FilterAccessId = accessId; RaiseStateChanged(); }

    /// <summary>Sets the category filter (does not reload).</summary>
    public void SetFilterCategory(short? category) { FilterCategory = category; RaiseStateChanged(); }

    /// <summary>Sets the result filter (does not reload).</summary>
    public void SetFilterResult(string? result) { FilterResult = string.IsNullOrWhiteSpace(result) ? null : result.Trim(); RaiseStateChanged(); }

    /// <summary>Sets the tool filter (does not reload).</summary>
    public void SetFilterTool(string? toolName) { FilterToolName = string.IsNullOrWhiteSpace(toolName) ? null : toolName.Trim(); RaiseStateChanged(); }

    /// <summary>Sets the correlation-id filter from free text (does not reload).</summary>
    public void SetFilterCorrelation(string? correlation)
    {
        FilterCorrelationId = Guid.TryParse(correlation, out var id) ? id : null;
        RaiseStateChanged();
    }

    /// <summary>Sets the inclusive time range filter (does not reload).</summary>
    public void SetFilterRange(DateTime? from, DateTime? to) { FilterFrom = from; FilterTo = to; RaiseStateChanged(); }

    /// <summary>Whether the result filter currently equals <c>Denied</c> (quick-filter chip).</summary>
    public bool OnlyDenied => string.Equals(FilterResult, "Denied", StringComparison.Ordinal);

    /// <summary>Toggles the "nur Verweigertes" quick filter and reloads.</summary>
    public async Task ToggleDeniedOnlyAsync()
    {
        SetFilterResult(OnlyDenied ? null : "Denied");
        await ApplyFiltersAsync();
    }

    /// <summary>Initializes the view model; the card route carries no id.</summary>
    /// <param name="id">Unused — the audit view is not bound to one record.</param>
    public override async Task InitializeAsync(Guid id)
    {
        await LoadAsync(id);
    }

    /// <summary>
    /// Loads the access dropdown, applies deep-link query parameters (<c>accessId</c>,
    /// <c>correlationId</c>) and loads the first timeline page.
    /// </summary>
    /// <param name="id">Unused — the audit view is not bound to one record.</param>
    public override async Task LoadAsync(Guid id)
    {
        Loading = true; SetError(null, null); IsMcpDisabled = false; RaiseStateChanged();
        try
        {
            try { Accesses = await _api.Mcp_ListAccessesAsync(take: 200); }
            catch { Accesses = Array.Empty<McpAccessListItemDto>(); }

            ApplyDeepLink();

            Entries.Clear();
            ExpandedId = null; Detail = null;
            var page = await _api.Mcp_ListAuditEntriesAsync(
                FilterFrom, FilterTo, FilterCategory, FilterAccessId, FilterCorrelationId,
                FilterToolName, FilterResult, 0, PageSize, CancellationToken.None);
            Entries.AddRange(page);
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

    /// <summary>
    /// Reads deep-link query parameters (<c>accessId</c>, <c>correlationId</c>) from the
    /// current navigation URI and applies them to the pending filters — the first load
    /// honours them (ui-konzept.md §4.5: Deep-Link mit Zugangsfilter).
    /// </summary>
    private void ApplyDeepLink()
    {
        try
        {
            var nav = ServiceProvider.GetRequiredService<NavigationManager>();
            var q = QueryHelpers.ParseQuery(nav.ToAbsoluteUri(nav.Uri).Query);
            if (q.TryGetValue("accessId", out var acc) && Guid.TryParse(acc.ToString(), out var accessId))
            {
                FilterAccessId = accessId;
            }
            if (q.TryGetValue("correlationId", out var corr) && Guid.TryParse(corr.ToString(), out var correlationId))
            {
                FilterCorrelationId = correlationId;
            }
        }
        catch { /* deep link is optional */ }
    }

    /// <summary>Applies the pending filters and reloads the first timeline page.</summary>
    public async Task ApplyFiltersAsync()
    {
        await LoadAsync(Guid.Empty);
    }

    /// <summary>Clears all filters and reloads.</summary>
    public async Task ClearFiltersAsync()
    {
        FilterAccessId = null; FilterCategory = null; FilterResult = null;
        FilterToolName = null; FilterCorrelationId = null; FilterFrom = null; FilterTo = null;
        await LoadAsync(Guid.Empty);
    }

    /// <summary>Loads the next timeline page and appends it.</summary>
    public async Task LoadMoreAsync()
    {
        if (!CanLoadMore) return;
        Loading = true; RaiseStateChanged();
        try
        {
            var page = await _api.Mcp_ListAuditEntriesAsync(
                FilterFrom, FilterTo, FilterCategory, FilterAccessId, FilterCorrelationId,
                FilterToolName, FilterResult, Entries.Count, PageSize, CancellationToken.None);
            Entries.AddRange(page);
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
    /// Expands an entry (loads its detail incl. field deltas and payload) or collapses the
    /// currently expanded one (drilling, audit-konzept.md §6.1).
    /// </summary>
    /// <param name="entryId">Entry id.</param>
    public async Task ToggleDetailsAsync(Guid entryId)
    {
        if (ExpandedId == entryId)
        {
            ExpandedId = null; Detail = null; RaiseStateChanged();
            return;
        }
        ExpandedId = entryId; Detail = null; RaiseStateChanged();
        try
        {
            Detail = await _api.Mcp_GetAuditEntryAsync(entryId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
        }
        RaiseStateChanged();
    }

    /// <summary>
    /// Runs the hash-chain verification ("Integrität prüfen", audit-konzept.md §7.2) and
    /// stores the result for display.
    /// </summary>
    public async Task VerifyAsync()
    {
        if (Verifying) return;
        Verifying = true; RaiseStateChanged();
        try
        {
            Verification = await _api.Mcp_VerifyAuditChainAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Verification = null;
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
        }
        finally { Verifying = false; RaiseStateChanged(); }
    }

    /// <summary>
    /// Builds the export download URL of the current filter state (csv/json). The download
    /// itself runs via the panel's JS interop — the endpoint uses cookie auth like the
    /// statement-draft exports.
    /// </summary>
    /// <param name="format"><c>csv</c> or <c>json</c>.</param>
    /// <returns>The relative export URL.</returns>
    public string BuildExportUrl(string format)
        => _api.Mcp_BuildAuditExportUrl(format, FilterFrom, FilterTo, FilterCategory,
            FilterAccessId, FilterCorrelationId, FilterToolName, FilterResult);

    /// <summary>
    /// Groups the loaded entries by event date for the timeline rendering
    /// (Zeit/Kategorie-Gruppierung).
    /// </summary>
    /// <returns>Entries grouped by <c>Utc</c> date, descending.</returns>
    public IEnumerable<IGrouping<DateTime, McpAuditEntryListItemDto>> DayGroups()
        => Entries.GroupBy(e => e.Utc.Date).OrderByDescending(g => g.Key);

    /// <summary>
    /// Requests the audit timeline panel rendered below the (empty) card area.
    /// </summary>
    /// <param name="position">Requested panel position.</param>
    /// <returns>Embedded panel specs for the requested position.</returns>
    public override IReadOnlyList<EmbeddedPanelSpec> GetEmbeddedPanelSpecs(EmbeddedPanelPosition position)
    {
        if (position != EmbeddedPanelPosition.AfterCard)
        {
            return Array.Empty<EmbeddedPanelSpec>();
        }
        var parms = new Dictionary<string, object?> { ["Provider"] = this };
        return new[]
        {
            new EmbeddedPanelSpec(typeof(McpAuditPanel), parms, EmbeddedPanelPosition.AfterCard, Visible: true)
        };
    }

    /// <summary>
    /// Provides the ribbon: Back (to the access list) and Reload.
    /// </summary>
    /// <param name="localizer">Localizer used to resolve labels.</param>
    /// <returns>Ribbon registers for the audit view.</returns>
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var navigation = new List<UiRibbonAction>
        {
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>",
                UiRibbonItemSize.Large, false, null,
                () => { RaiseUiActionRequested("Back", "/list/mcp-accesses"); return Task.CompletedTask; })
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

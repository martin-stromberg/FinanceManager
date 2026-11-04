using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Mcp;

/// <summary>
/// Represents a single MCP access row in the access list and provides navigation to the access card.
/// </summary>
/// <param name="Id">Identifier of the access.</param>
/// <param name="Name">Display name of the access.</param>
/// <param name="Level">Access level (McpAccessLevel as short).</param>
/// <param name="Status">Lifecycle status (McpAccessStatus as short).</param>
/// <param name="IsExpired">Whether the access is currently expired.</param>
/// <param name="ExpiresUtc">Optional expiry timestamp.</param>
/// <param name="LastUsedUtc">Last successful authentication timestamp.</param>
public sealed record McpAccessListItem(
    Guid Id,
    string Name,
    short Level,
    short Status,
    bool IsExpired,
    DateTime? ExpiresUtc,
    DateTime? LastUsedUtc) : IListItemNavigation
{
    /// <summary>
    /// Returns the navigation URL for the access detail card.
    /// </summary>
    /// <returns>Relative URL to the MCP access card.</returns>
    public string GetNavigateUrl() => $"/card/mcp-accesses/{Id}";
}

/// <summary>
/// View model for the MCP access list (<c>/list/mcp-accesses</c>). Loads the accesses of the
/// current user through <see cref="FinanceManager.Shared.IApiClient"/> and exposes localized
/// level/status texts for the generic list renderer. When the server reports
/// <c>Err_Mcp_Disabled</c> the list shows a friendly hint instead of an error.
/// </summary>
public sealed class McpAccessListViewModel : BaseListViewModel<McpAccessListItem>
{
    /// <summary>
    /// Initializes a new instance of <see cref="McpAccessListViewModel"/>.
    /// </summary>
    /// <param name="sp">Service provider used to resolve dependencies (localizer, api client provided by base).</param>
    public McpAccessListViewModel(IServiceProvider sp)
        : base(sp)
    {
    }

    /// <summary>
    /// Indicates whether the last load reported <c>Err_Mcp_Disabled</c> (MCP feature disabled server-side).
    /// The list then renders a friendly hint record instead of data rows.
    /// </summary>
    public bool IsMcpDisabled { get; private set; }

    /// <summary>
    /// Loads a page of MCP accesses from the API. Search text is forwarded to the server.
    /// </summary>
    /// <param name="resetPaging">When <c>true</c> the paging offset is reset; otherwise the next page is appended.</param>
    /// <returns>A task that completes when loading has finished.</returns>
    protected override async Task LoadPageAsync(bool resetPaging)
    {
        IsMcpDisabled = false;
        try
        {
            var skip = resetPaging ? 0 : Items.Count;
            var list = await ApiClient.Mcp_ListAccessesAsync(
                string.IsNullOrWhiteSpace(Search) ? null : Search,
                skip,
                100,
                CancellationToken.None);
            Items.AddRange(list.Select(d => new McpAccessListItem(
                d.Id, d.Name, d.Level, d.Status, d.IsExpired, d.ExpiresUtc, d.LastUsedUtc)));
            CanLoadMore = list.Count >= 100;
        }
        catch (Exception ex)
        {
            CanLoadMore = false;
            if (string.Equals(ApiClient.LastErrorCode, "Err_Mcp_Disabled", StringComparison.Ordinal))
            {
                IsMcpDisabled = true;
            }
            SetError(ApiClient.LastErrorCode, ApiClient.LastError ?? ex.Message);
        }
    }

    /// <summary>
    /// Builds column definitions and rendered records (localized level/status texts, expiry/usage dates).
    /// </summary>
    protected override void BuildRecords()
    {
        var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();
        Columns = new List<ListColumn>
        {
            new ListColumn("name", L["McpList_Th_Name"].Value ?? "Name", null, ListColumnAlign.Left),
            new ListColumn("level", L["McpList_Th_Level"].Value ?? "Level", "180px", ListColumnAlign.Left),
            new ListColumn("status", L["McpList_Th_Status"].Value ?? "Status", "110px", ListColumnAlign.Left),
            new ListColumn("expires", L["McpList_Th_Expires"].Value ?? "Expires", "160px", ListColumnAlign.Left),
            new ListColumn("lastUsed", L["McpList_Th_LastUsed"].Value ?? "LastUsed", "160px", ListColumnAlign.Left)
        };

        if (IsMcpDisabled)
        {
            Records = new List<ListRecord>
            {
                new ListRecord(
                    new List<ListCell>
                    {
                        new ListCell(ListCellKind.Text, Text: L["Mcp_Disabled_Hint"].Value ?? "MCP is disabled.", Muted: true),
                        new ListCell(ListCellKind.Text, Text: string.Empty, Muted: true),
                        new ListCell(ListCellKind.Text, Text: string.Empty, Muted: true),
                        new ListCell(ListCellKind.Text, Text: string.Empty, Muted: true),
                        new ListCell(ListCellKind.Text, Text: string.Empty, Muted: true)
                    },
                    Item: null)
            };
            return;
        }

        Records = Items.Select(i =>
        {
            var muted = i.IsExpired || (McpAccessStatus)i.Status != McpAccessStatus.Active;
            return new ListRecord(new List<ListCell>
            {
                new ListCell(ListCellKind.Text, Text: i.Name, Muted: muted),
                new ListCell(ListCellKind.Text, Text: McpAccessText.Level(L, i.Level), Muted: muted),
                new ListCell(ListCellKind.Text, Text: McpAccessText.Status(L, i.Status, i.IsExpired), Muted: muted),
                new ListCell(ListCellKind.Text, Text: i.ExpiresUtc?.ToString("u") ?? "-", Muted: muted),
                new ListCell(ListCellKind.Text, Text: i.LastUsedUtc?.ToString("u") ?? (L["Mcp_Value_NeverUsed"].Value ?? "-"), Muted: muted)
            }, i);
        }).ToList();
    }

    /// <summary>
    /// Provides ribbon registers for the MCP access list: a create-wizard action and a reload action.
    /// </summary>
    /// <param name="localizer">Localizer used to resolve UI labels.</param>
    /// <returns>A collection of <see cref="UiRibbonRegister"/> instances.</returns>
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var actions = new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "New",
                localizer["Ribbon_New"].Value,
                "<svg><use href='/icons/sprite.svg#plus'/></svg>",
                UiRibbonItemSize.Large,
                false,
                null,
                new Func<Task>(() =>
                {
                    var parms = new Dictionary<string, object?>
                    {
                        ["OverlayTitle"] = localizer["McpWizard_Title"].Value
                    };
                    RaiseUiActionRequested("New", new UiOverlaySpec(typeof(McpAccessCreateWizard), parms));
                    return Task.CompletedTask;
                }))
                { MobileShortcut = true },
            new UiRibbonAction(
                "AuditLog",
                localizer["Mcp_Ribbon_AuditLog"].Value,
                "<svg><use href='/icons/sprite.svg#list'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                new Func<Task>(() =>
                {
                    // /card/mcp-audit — the audit timeline is a card-route view without record id.
                    ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()
                        .NavigateTo("/card/mcp-audit");
                    return Task.CompletedTask;
                })),
            new UiRibbonAction(
                "Reload",
                localizer["Ribbon_Reload"].Value,
                "<svg><use href='/icons/sprite.svg#refresh'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                new Func<Task>(async () => { await LoadAsync(); }))
        };

        var tabs = new List<UiRibbonTab> { new UiRibbonTab(localizer["Ribbon_Group_Manage"].Value, actions) };
        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }
}

/// <summary>
/// Shared localization helpers for MCP access level, status and approval mode texts used by
/// list, card and wizard UI. Keeps the mapping from the numeric DTO values to resource keys
/// in a single place.
/// </summary>
public static class McpAccessText
{
    /// <summary>Resource key prefix for level names.</summary>
    public const string LevelKeyPrefix = "Mcp_Level_";

    /// <summary>Resource key prefix for level explanations.</summary>
    public const string LevelDescriptionKeyPrefix = "Mcp_LevelDesc_";

    /// <summary>
    /// Returns the enum name of an <see cref="McpAccessLevel"/> transported as <see langword="short"/>.
    /// </summary>
    /// <param name="level">Level value from the DTO.</param>
    /// <returns>The enum member name or the raw value as string.</returns>
    public static string LevelName(short level)
        => Enum.IsDefined(typeof(McpAccessLevel), (int)level)
            ? ((McpAccessLevel)level).ToString()
            : level.ToString();

    /// <summary>
    /// Returns the localized display name of an access level.
    /// </summary>
    /// <param name="localizer">Localizer for the <c>Pages</c> resources.</param>
    /// <param name="level">Level value from the DTO.</param>
    /// <returns>Localized level name or the raw value.</returns>
    public static string Level(IStringLocalizer localizer, short level)
        => Lookup(localizer, LevelKeyPrefix + LevelName(level), LevelName(level));

    /// <summary>
    /// Returns the localized explanation of an access level.
    /// </summary>
    /// <param name="localizer">Localizer for the <c>Pages</c> resources.</param>
    /// <param name="level">Level value from the DTO.</param>
    /// <returns>Localized level explanation or an empty string.</returns>
    public static string LevelDescription(IStringLocalizer localizer, short level)
        => Lookup(localizer, LevelDescriptionKeyPrefix + LevelName(level), string.Empty);

    /// <summary>
    /// Returns the localized status text including the derived expired state.
    /// </summary>
    /// <param name="localizer">Localizer for the <c>Pages</c> resources.</param>
    /// <param name="status">Status value from the DTO (McpAccessStatus as short).</param>
    /// <param name="isExpired">Whether the access is currently expired.</param>
    /// <returns>Localized status text.</returns>
    public static string Status(IStringLocalizer localizer, short status, bool isExpired)
    {
        if (isExpired && (McpAccessStatus)status == McpAccessStatus.Active)
        {
            return Lookup(localizer, "Mcp_Status_Expired", "Expired");
        }
        var name = Enum.IsDefined(typeof(McpAccessStatus), (int)status)
            ? ((McpAccessStatus)status).ToString()
            : status.ToString();
        return Lookup(localizer, "Mcp_Status_" + name, name);
    }

    /// <summary>
    /// Returns the localized approval mode text.
    /// </summary>
    /// <param name="localizer">Localizer for the <c>Pages</c> resources.</param>
    /// <param name="approvalMode">Approval mode value from the DTO (McpApprovalMode as short).</param>
    /// <returns>Localized approval mode text.</returns>
    public static string ApprovalMode(IStringLocalizer localizer, short approvalMode)
    {
        var name = Enum.IsDefined(typeof(McpApprovalMode), (int)approvalMode)
            ? ((McpApprovalMode)approvalMode).ToString()
            : approvalMode.ToString();
        return Lookup(localizer, "Mcp_ApprovalMode_" + name, name);
    }

    /// <summary>
    /// Returns the localized right kind text (McpRightKind as short).
    /// </summary>
    /// <param name="localizer">Localizer for the <c>Pages</c> resources.</param>
    /// <param name="right">Right kind value.</param>
    /// <returns>Localized right kind text.</returns>
    public static string RightKind(IStringLocalizer localizer, short right)
    {
        var name = Enum.IsDefined(typeof(McpRightKind), (int)right)
            ? ((McpRightKind)right).ToString()
            : right.ToString();
        return Lookup(localizer, "Mcp_Right_" + name, name);
    }

    /// <summary>
    /// Returns the localized data type text (McpDataType as short).
    /// </summary>
    /// <param name="localizer">Localizer for the <c>Pages</c> resources.</param>
    /// <param name="dataType">Data type value.</param>
    /// <returns>Localized data type text.</returns>
    public static string DataType(IStringLocalizer localizer, short dataType)
    {
        var name = Enum.IsDefined(typeof(McpDataType), (int)dataType)
            ? ((McpDataType)dataType).ToString()
            : dataType.ToString();
        return Lookup(localizer, "Mcp_DataType_" + name, name);
    }

    private static string Lookup(IStringLocalizer localizer, string key, string fallback)
    {
        var value = localizer[key];
        return value.ResourceNotFound || string.IsNullOrWhiteSpace(value.Value) ? fallback : value.Value;
    }
}

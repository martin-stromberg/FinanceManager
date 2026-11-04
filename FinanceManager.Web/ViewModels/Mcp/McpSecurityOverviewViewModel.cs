using FinanceManager.Domain.Attachments;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Mcp;

/// <summary>
/// Admin security-overview view model (<c>/card/mcp-security</c>, F-1005, ui-konzept.md §5.2):
/// loads the instance-wide, metadata-only MCP security overview — global switch state,
/// access metadata of every owner, pending counters and recent security events. No
/// business content of foreign users is shown (audit-konzept.md §6.2).
/// </summary>
[CardRoute("mcp-security")]
public sealed class McpSecurityOverviewViewModel : BaseCardViewModel<(string Key, string Value)>
{
    private readonly IApiClient _api;

    /// <summary>Initializes a new instance of <see cref="McpSecurityOverviewViewModel"/>.</summary>
    public McpSecurityOverviewViewModel(IServiceProvider sp, IApiClient apiClient) : base(sp)
    {
        _api = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    /// <summary>Title shown on the card header.</summary>
    public override string Title => Localizer?["McpSecurity_Title"].Value ?? "MCP security";

    /// <summary>The loaded overview, or <see langword="null"/> while loading/unauthorized.</summary>
    public McpSecurityOverviewDto? Overview { get; private set; }

    /// <summary>Whether the caller lacks the Admin role (endpoint answered 403).</summary>
    public bool NotAuthorized { get; private set; }

    /// <inheritdoc />
    public override async Task InitializeAsync(Guid id)
    {
        await LoadAsync(id);
    }

    /// <inheritdoc />
    public override async Task LoadAsync(Guid id)
    {
        Loading = true; SetError(null, null); NotAuthorized = false; RaiseStateChanged();
        try
        {
            Overview = await _api.Admin_McpSecurityOverviewAsync(50, CancellationToken.None);
            NotAuthorized = Overview is null;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
        }
        finally { Loading = false; RaiseStateChanged(); }
    }

    /// <summary>Requests the security-overview panel rendered below the card area.</summary>
    public override IReadOnlyList<EmbeddedPanelSpec> GetEmbeddedPanelSpecs(EmbeddedPanelPosition position)
    {
        if (position != EmbeddedPanelPosition.AfterCard)
        {
            return Array.Empty<EmbeddedPanelSpec>();
        }
        var parms = new Dictionary<string, object?> { ["Provider"] = this };
        return new[]
        {
            new EmbeddedPanelSpec(typeof(McpSecurityOverviewPanel), parms, EmbeddedPanelPosition.AfterCard, Visible: true)
        };
    }

    /// <summary>Ribbon: Back (to the access list) and Reload.</summary>
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

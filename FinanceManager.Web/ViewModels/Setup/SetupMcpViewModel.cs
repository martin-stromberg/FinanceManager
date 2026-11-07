using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Web.Services;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Setup;

/// <summary>
/// Setup-section view model for the KI (MCP) register inside the setup card.
/// Hosts the admin feature switches (Enabled / WriteAccessEnabled, persisted via
/// <c>PUT /api/mcp-audit/settings</c>) and embeds the <see cref="McpAccessListViewModel"/>
/// so the register shows the current user's own AI accesses. The section is only
/// reachable when the user is an admin or the MCP feature is enabled — the
/// <see cref="SetupCardViewModel"/> gates registration/visibility accordingly.
/// </summary>
public sealed class SetupMcpViewModel : BaseViewModel
{
    private readonly ICurrentUserService? _currentUser;
    private bool _persistedWriteAccess;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetupMcpViewModel"/> class.
    /// Creates the embedded access list view model and forwards its UI action
    /// requests (for example the create-wizard overlay) to this view model's own
    /// <see cref="BaseViewModel.UiActionRequested"/> event so the hosting card
    /// page can render them.
    /// </summary>
    /// <param name="sp">The service provider used to resolve required services.</param>
    public SetupMcpViewModel(IServiceProvider sp) : base(sp)
    {
        _currentUser = sp.GetService<ICurrentUserService>();
        Accesses = ActivatorUtilities.CreateInstance<McpAccessListViewModel>(sp);
        Accesses.UiActionRequested += (_, args) => ForwardUiAction(args);
    }

    /// <summary>
    /// Indicates whether the current user has administrative privileges.
    /// The admin part (feature switches) is only rendered for admins.
    /// </summary>
    public bool IsAdmin => _currentUser?.IsAuthenticated == true && _currentUser.IsAdmin;

    /// <summary>
    /// Indicates whether the MCP feature is enabled instance-wide.
    /// The value is provided by the owning <see cref="SetupCardViewModel"/> via
    /// <see cref="InitializeFeatureState"/> and controls whether the access list
    /// or a disabled hint is shown.
    /// </summary>
    public bool McpEnabled { get; private set; }

    /// <summary>
    /// Number of pending approval requests of the current user (best effort, 0 when unknown).
    /// Used to surface a pending counter on the ribbon "Approvals" action.
    /// </summary>
    public int PendingApprovalCount { get; private set; }

    /// <summary>
    /// The embedded list of the current user's MCP accesses shown inside the register.
    /// </summary>
    public McpAccessListViewModel Accesses { get; }

    /// <summary>
    /// Pending edit value of the MCP kill switch (<see cref="McpSettingsDto.Enabled"/>),
    /// initialized from the persisted settings.
    /// </summary>
    public bool SettingsEnabled { get; private set; }

    /// <summary>
    /// Pending edit value of the write-stop lid (<see cref="McpSettingsDto.WriteAccessEnabled"/>),
    /// initialized from the persisted settings.
    /// </summary>
    public bool SettingsWriteAccess { get; private set; }

    /// <summary>
    /// Whether a settings save is currently running.
    /// </summary>
    public bool SettingsBusy { get; private set; }

    /// <summary>
    /// Provides the feature state resolved by the owning setup card (MCP enabled flag and
    /// pending approval counter). Called once right after the view model was created so no
    /// duplicate status call is needed.
    /// </summary>
    /// <param name="mcpEnabled">Whether the MCP feature is enabled instance-wide.</param>
    /// <param name="pendingApprovalCount">Pending approval requests of the current user.</param>
    public void InitializeFeatureState(bool mcpEnabled, int pendingApprovalCount)
    {
        McpEnabled = mcpEnabled;
        PendingApprovalCount = pendingApprovalCount;
        RaiseStateChanged();
    }

    /// <summary>
    /// Loads the register content: for admins the persisted MCP settings
    /// (<c>GET /api/mcp-audit/settings</c>) and always the embedded access list.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the reload has finished.</returns>
    public async Task ReloadAsync(CancellationToken ct = default)
    {
        SetError(null, null);
        if (IsAdmin)
        {
            try
            {
                var settings = await ApiClient.Admin_McpGetSettingsAsync(ct);
                if (settings is not null)
                {
                    SettingsEnabled = settings.Enabled;
                    SettingsWriteAccess = settings.WriteAccessEnabled;
                    _persistedWriteAccess = settings.WriteAccessEnabled;
                }
            }
            catch (Exception ex)
            {
                SetError(ApiClient.LastErrorCode, ApiClient.LastError ?? ex.Message);
            }
        }

        try
        {
            // The list self-guards against a disabled feature (Err_Mcp_Disabled)
            // and is only rendered when McpEnabled is true.
            await Accesses.InitializeAsync();
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode, ApiClient.LastError ?? ex.Message);
        }
        RaiseStateChanged();
    }

    /// <summary>Updates the pending kill-switch value (does not persist).</summary>
    /// <param name="enabled">New pending value.</param>
    public void SetSettingsEnabled(bool enabled) { SettingsEnabled = enabled; RaiseStateChanged(); }

    /// <summary>Updates the pending write-access value (does not persist).</summary>
    /// <param name="enabled">New pending value.</param>
    public void SetSettingsWriteAccess(bool enabled) { SettingsWriteAccess = enabled; RaiseStateChanged(); }

    /// <summary>
    /// Persists the pending MCP settings (<c>PUT /api/mcp-audit/settings</c>). Enabling the
    /// write-stop lid runs through a warning confirmation — it unlocks every write-capable
    /// tool class for all accesses.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the settings were persisted.</returns>
    public async Task<bool> SaveSettingsAsync(CancellationToken ct = default)
    {
        if (!IsAdmin || SettingsBusy)
        {
            return false;
        }

        var enablingWriteAccess = SettingsWriteAccess && !_persistedWriteAccess;
        if (enablingWriteAccess)
        {
            var confirmed = await ConfirmationService.ConfirmAsync(new(
                TitleResourceKey: "Confirmation_McpWriteAccess_Title",
                MessageResourceKey: "Confirmation_McpWriteAccess_Message",
                Severity: ConfirmationSeverity.Warning), ct);
            if (!confirmed)
            {
                return false;
            }
        }

        SettingsBusy = true; SetError(null, null); RaiseStateChanged();
        try
        {
            await ApiClient.Admin_McpUpdateSettingsAsync(
                new McpSettingsUpdateRequest(SettingsEnabled, SettingsWriteAccess), ct);
            await ReloadAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode, ApiClient.LastError ?? ex.Message);
            return false;
        }
        finally { SettingsBusy = false; RaiseStateChanged(); }
    }

    /// <summary>
    /// Requests the create-access wizard overlay. Delegates to the embedded list view model
    /// so list page and register share the same flow.
    /// </summary>
    public void RequestNewAccess() => Accesses.RequestCreateWizard();

    /// <summary>
    /// Provides the dedicated KI ribbon group with navigation to the approval center,
    /// the audit log and — for admins only — the security overview.
    /// </summary>
    /// <param name="localizer">Localizer used to resolve UI labels.</param>
    /// <returns>A collection of <see cref="UiRibbonRegister"/> instances.</returns>
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var approvalsLabel = localizer["Nav_McpApprovals"].Value;
        if (PendingApprovalCount > 0)
        {
            approvalsLabel = $"{approvalsLabel} ({PendingApprovalCount})";
        }

        var actions = new List<UiRibbonAction>
        {
            new UiRibbonAction(
                "McpApprovals",
                approvalsLabel,
                "<svg><use href='/icons/sprite.svg#check'/></svg>",
                UiRibbonItemSize.Large,
                false,
                null,
                new Func<Task>(() =>
                {
                    Navigation.NavigateTo("/card/mcp-approvals");
                    return Task.CompletedTask;
                })),
            new UiRibbonAction(
                "McpAudit",
                localizer["Mcp_Ribbon_AuditLog"].Value,
                "<svg><use href='/icons/sprite.svg#list'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                new Func<Task>(() =>
                {
                    Navigation.NavigateTo("/card/mcp-audit");
                    return Task.CompletedTask;
                })),
            new UiRibbonAction(
                "McpSecurity",
                localizer["Nav_McpSecurity"].Value,
                "<svg><use href='/icons/sprite.svg#key'/></svg>",
                UiRibbonItemSize.Small,
                false,
                null,
                new Func<Task>(() =>
                {
                    Navigation.NavigateTo("/card/mcp-security");
                    return Task.CompletedTask;
                }))
            { Hidden = !IsAdmin }
        };

        var tabs = new List<UiRibbonTab>
        {
            new UiRibbonTab(localizer["Ribbon_Group_Mcp"].Value, actions)
        };
        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }

    private void ForwardUiAction(UiActionEventArgs? args)
    {
        if (args is null)
        {
            return;
        }

        if (args.PayloadObject is not null)
        {
            RaiseUiActionRequested(args.Action, args.PayloadObject);
        }
        else
        {
            RaiseUiActionRequested(args.Action, args.Payload);
        }
    }
}

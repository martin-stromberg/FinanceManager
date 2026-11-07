using FinanceManager.Application.Backups;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Notifications;
using FinanceManager.Domain.Mcp;
using FinanceManager.Domain.Users;
using FinanceManager.Web.Services.Updates;
using Microsoft.AspNetCore.Identity;
using System.Globalization;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the read tool <c>fm_admin_status</c> (T34): installation status of the
/// own instance — installed release, update stand, last backup and the owner's active
/// notification counter, plus the effective MCP switches. Admin-gated (the owner must
/// hold the admin role, <c>Err_Mcp_AdminRequired</c> otherwise).
/// Scope: (Administration, Lesen).
/// </summary>
public sealed class FmAdminStatusDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_admin_status";

    /// <summary>Creates the descriptor.</summary>
    public FmAdminStatusDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Reads the installation status of the own instance: installed release metadata, " +
           "update status (available update, last check, lock, scheduled install time), latest " +
           "backup, active notification count and the effective MCP kill-switch/write-stop " +
           "states. Restricted to owners with administrative privileges. " +
           "Scope: (Administration, Lesen).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Administration, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_admin_status</c>: adapts <see cref="IInstalledReleaseMetadataProvider"/>,
/// <see cref="IUpdateOrchestrator"/>, <see cref="IBackupService"/>,
/// <see cref="INotificationService"/> and <see cref="IMcpSettingsService"/>; the admin gate
/// runs through <see cref="UserManager{User}"/> (owner-based Identity role check).
/// Externally sourced texts (release notes,
/// error messages, file names) are untrusted content (<c>untrusted_data</c>).
/// </summary>
public sealed class FmAdminStatusHandler : IMcpToolHandler
{
    private readonly UserManager<User> _users;
    private readonly IInstalledReleaseMetadataProvider _release;
    private readonly IUpdateOrchestrator _updates;
    private readonly IBackupService _backups;
    private readonly INotificationService _notifications;
    private readonly IMcpSettingsService _mcpSettings;

    /// <summary>Creates the handler.</summary>
    public FmAdminStatusHandler(
        UserManager<User> users,
        IInstalledReleaseMetadataProvider release,
        IUpdateOrchestrator updates,
        IBackupService backups,
        INotificationService notifications,
        IMcpSettingsService mcpSettings)
    {
        _users = users;
        _release = release;
        _updates = updates;
        _backups = backups;
        _notifications = notifications;
        _mcpSettings = mcpSettings;
    }

    /// <inheritdoc />
    public string ToolName => FmAdminStatusDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        await McpAdminGuards.RequireAdminAsync(_users, owner, ct);

        var release = await _release.GetAsync(ct);
        var status = await _updates.GetStatusAsync(ct);
        var backups = await _backups.ListAsync(owner, ct);
        var notifications = await _notifications.ListActiveAsync(owner, DateTime.UtcNow, ct);
        var mcp = await _mcpSettings.GetAsync(ct);

        var latest = backups.Count == 0 ? null : backups.OrderByDescending(b => b.CreatedUtc).First();

        return new
        {
            installation = new
            {
                version = release.Version,
                publishedAt = release.PublishedAt,
                commitSha = release.CommitSha,
                runtimeIdentifier = release.RuntimeIdentifier,
                untrusted_data = new { repository = release.Repository }
            },
            update = new
            {
                status = status.Status.ToString(),
                installedVersion = status.InstalledVersion,
                availableVersion = status.AvailableVersion,
                currentPlatform = status.CurrentPlatform,
                lastCheckedAt = status.LastCheckedAt,
                isLocked = status.IsLocked,
                lockCreatedAt = status.LockCreatedAt,
                scheduledInstallTime = status.ScheduledInstallTime?.ToString("HH:mm", CultureInfo.InvariantCulture),
                untrusted_data = new
                {
                    lastError = status.LastError,
                    downloadedAssetName = status.DownloadedAssetName
                }
            },
            backups = new
            {
                count = backups.Count,
                latest = latest is null ? null : new
                {
                    id = latest.Id,
                    createdUtc = latest.CreatedUtc,
                    sizeBytes = latest.SizeBytes,
                    untrusted_data = new { fileName = latest.FileName, source = latest.Source }
                }
            },
            notifications = new { activeCount = notifications.Count },
            mcp = new { enabled = mcp.Enabled, writeAccessEnabled = mcp.WriteAccessEnabled }
        };
    }
}

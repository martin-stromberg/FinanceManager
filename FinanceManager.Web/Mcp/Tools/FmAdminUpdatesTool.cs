using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Domain.Users;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using Microsoft.AspNetCore.Identity;
using System.Globalization;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the administrative update tool <c>fm_admin_updates</c> (T35): reads the
/// update status and triggers a source check; installing the downloaded update and
/// changing the scheduled installation time are final/effective sub-operations that run
/// through the approval queue (<c>Err_Mcp_ApprovalRequired</c> → <c>AiActionRequest</c>).
/// Admin-gated (the owner must hold the admin role, <c>Err_Mcp_AdminRequired</c> otherwise).
/// Scope: (Administration, Lesen); install/schedule additionally (Administration, Änderung)
/// — evaluated inside the handler.
/// </summary>
public sealed class FmAdminUpdatesDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_admin_updates";

    /// <summary>Creates the descriptor.</summary>
    public FmAdminUpdatesDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"operation\":{\"type\":\"string\",\"enum\":[\"status\",\"check\",\"install\",\"schedule\"],\"description\":\"Update operation (default 'status'); install and schedule require approval.\"},\"confirmDowntime\":{\"type\":\"boolean\",\"description\":\"Required true for install — the installation restarts the application.\"},\"scheduledInstallTime\":{\"type\":\"string\",\"description\":\"New scheduled installation time 'HH:mm' (schedule); explicit null clears the schedule.\"},\"idempotencyKey\":{\"type\":\"string\",\"description\":\"Client deduplication key for the approval request.\"},\"reasonText\":{\"type\":\"string\",\"description\":\"Optional client reason text (untrusted).\"}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Reads the update status (default operation 'status') or triggers a source check " +
           "('check' — checks the configured repository and downloads an available package). " +
           "'install' starts the installation of the downloaded update (restarts the " +
           "application; requires confirmDowntime) and 'schedule' sets/clears the scheduled " +
           "installation time — both are final/effective and run through the approval queue. " +
           "Restricted to owners with administrative privileges. Scope: (Administration, " +
           "Lesen); install/schedule additionally (Administration, Änderung).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Schreibvorbereitend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Administration, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_admin_updates</c>: adapts <see cref="IUpdateOrchestrator"/> (the same
/// contract the admin REST endpoint uses). Release texts, error messages and asset names
/// are untrusted content (<c>untrusted_data</c>). Install/schedule stage their
/// <c>DataChange</c> record inside the effect path (F-0301) and re-evaluate admin gate and
/// scope at approved-execution time.
/// </summary>
public sealed class FmAdminUpdatesHandler : IMcpToolHandler
{
    private const string EntityType = "UpdateInstallation";

    private readonly UserManager<User> _users;
    private readonly IUpdateOrchestrator _updates;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;

    /// <summary>Creates the handler.</summary>
    public FmAdminUpdatesHandler(
        UserManager<User> users,
        IUpdateOrchestrator updates,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter)
    {
        _users = users;
        _updates = updates;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
    }

    /// <inheritdoc />
    public string ToolName => FmAdminUpdatesDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        await McpAdminGuards.RequireAdminAsync(_users, owner, ct);

        var operation = McpToolArguments.OptionalString(arguments, "operation", 30) ?? "status";
        return operation switch
        {
            "status" => await StatusAsync(ct),
            "check" => await CheckAsync(ct),
            "install" => await InstallAsync(arguments, context, ct),
            "schedule" => await ScheduleAsync(arguments, context, ct),
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'operation' must be one of status|check|install|schedule.")
        };
    }

    private async Task<object> StatusAsync(CancellationToken ct)
    {
        var status = await _updates.GetStatusAsync(ct);
        return new { operation = "status", update = MapStatus(status) };
    }

    private async Task<object> CheckAsync(CancellationToken ct)
    {
        var result = await _updates.CheckAsync(ct);
        return new
        {
            operation = "check",
            updateAvailable = result.UpdateAvailable,
            update = MapStatus(result.Status),
            untrusted_data = new { message = result.Message }
        };
    }

    // ------------------------------------------------------------------
    // install / schedule — genehmigungspflichtig (final/effective effects)
    // ------------------------------------------------------------------

    private async Task<object> InstallAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var confirmDowntime = McpToolArguments.OptionalBool(arguments, "confirmDowntime") ?? false;
        if (!confirmDowntime)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'confirmDowntime' must be true for install — the installation restarts the application.");
        }

        return await McpAdminGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.Administration, McpRightKind.Modify,
            "install",
            "Starts the installation of the downloaded update package: the application " +
            "restarts into the new version (downtime for every connected client).",
            new { operation = "install" }, ct,
            executeApproved: async ct2 =>
            {
                // An install run is not a persisted record — the fachlich nächste anchor of
                // the delta is the deterministic id of the release being installed (no run id
                // exists on the update contract). The audit writer rejects empty entity ids.
                var before = await _updates.GetStatusAsync(ct2);
                var targetVersion = before.AvailableUpdate?.Version ?? before.AvailableVersion
                    ?? before.DownloadedAssetName ?? before.InstalledVersion ?? "unknown";
                var staged = await McpWriteAudit.StageDataChangeAsync(
                    _auditWriter, context, "UpdateInstallStarted", ToolName,
                    new List<McpAuditFieldChange>
                    {
                        new(EntityType, McpAuditAnchors.StableId($"{EntityType}:{targetVersion}"),
                            "InstallStarted", null, targetVersion)
                    },
                    null,
                    JsonSerializer.Serialize(new { operation = "install" }), ct2);
                UpdateStatusDto status;
                try
                {
                    status = await _updates.StartInstallAsync(confirmDowntime: true, ct2);
                }
                catch (Exception ex) when (ex is InvalidOperationException or IOException)
                {
                    McpWriteAudit.DiscardStaged(context, staged);
                    throw new McpToolException("Err_Mcp_InvalidState", ex.Message);
                }
                catch
                {
                    McpWriteAudit.DiscardStaged(context, staged);
                    throw;
                }
                return new { operation = "install", applied = true, update = MapStatus(status) };
            });
    }

    private async Task<object> ScheduleAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        if (!McpToolArguments.Has(arguments, "scheduledInstallTime"))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'scheduledInstallTime' is required for schedule (explicit null clears the schedule).");
        }
        TimeOnly? schedule = null;
        var raw = McpToolArguments.OptionalString(arguments, "scheduledInstallTime", 10);
        if (raw is not null)
        {
            if (!TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new McpToolException("Err_Mcp_InvalidArguments",
                    "Argument 'scheduledInstallTime' must be a 'HH:mm' time string or null.");
            }
            schedule = parsed;
        }

        return await McpAdminGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.Administration, McpRightKind.Modify,
            "schedule",
            $"Sets the scheduled automatic installation time to '{schedule?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "cleared"}'.",
            new { operation = "schedule", scheduledInstallTime = schedule?.ToString("HH:mm", CultureInfo.InvariantCulture) }, ct,
            executeApproved: async ct2 =>
            {
                // The changed record is the update-settings document — file-based
                // (settings.json), so the delta anchors on its deterministic singleton id.
                var staged = await McpWriteAudit.StageDataChangeAsync(
                    _auditWriter, context, "UpdateScheduleChanged", ToolName,
                    new List<McpAuditFieldChange>
                    {
                        new("UpdateSettings", McpAuditAnchors.StableId("UpdateSettings"),
                            "ScheduledInstallTime", null,
                            schedule?.ToString("HH:mm", CultureInfo.InvariantCulture))
                    },
                    null,
                    JsonSerializer.Serialize(new { operation = "schedule", scheduledInstallTime = raw }), ct2);
                UpdateSettingsDto settings;
                try
                {
                    settings = await _updates.ScheduleAsync(schedule, ct2);
                }
                catch (Exception ex) when (ex is InvalidOperationException or IOException)
                {
                    McpWriteAudit.DiscardStaged(context, staged);
                    throw new McpToolException("Err_Mcp_InvalidState", ex.Message);
                }
                catch
                {
                    McpWriteAudit.DiscardStaged(context, staged);
                    throw;
                }
                return new { operation = "schedule", applied = true, updateSettings = MapSettings(settings) };
            });
    }

    internal static object MapStatus(UpdateStatusDto s)
        => new
        {
            status = s.Status.ToString(),
            installedVersion = s.InstalledVersion,
            availableVersion = s.AvailableVersion,
            currentPlatform = s.CurrentPlatform,
            lastCheckedAt = s.LastCheckedAt,
            isLocked = s.IsLocked,
            lockCreatedAt = s.LockCreatedAt,
            scheduledInstallTime = s.ScheduledInstallTime?.ToString("HH:mm", CultureInfo.InvariantCulture),
            availableUpdate = s.AvailableUpdate is null ? null : new
            {
                version = s.AvailableUpdate.Version,
                publishedAt = s.AvailableUpdate.PublishedAt,
                assetCount = s.AvailableUpdate.Assets.Count,
                untrusted_data = new
                {
                    repositoryOwner = s.AvailableUpdate.RepositoryOwner,
                    repositoryName = s.AvailableUpdate.RepositoryName,
                    releaseNotes = s.AvailableUpdate.ReleaseNotes
                }
            },
            untrusted_data = new
            {
                lastError = s.LastError,
                downloadedAssetName = s.DownloadedAssetName
            }
        };

    internal static object MapSettings(UpdateSettingsDto s)
        => new
        {
            enabled = s.Enabled,
            includePrereleases = s.IncludePrereleases,
            sourceCheckStartTime = s.SourceCheckStartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            sourceCheckEndTime = s.SourceCheckEndTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            scheduledInstallTime = s.ScheduledInstallTime?.ToString("HH:mm", CultureInfo.InvariantCulture),
            healthTimeoutSeconds = s.HealthTimeoutSeconds,
            untrusted_data = new
            {
                repositoryOwner = s.RepositoryOwner,
                repositoryName = s.RepositoryName,
                manifestAssetName = s.ManifestAssetName,
                serviceName = s.ServiceName,
                executablePath = s.ExecutablePath,
                workingDirectory = s.WorkingDirectory
            }
        };
}

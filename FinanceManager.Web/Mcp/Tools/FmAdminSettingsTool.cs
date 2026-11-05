using FinanceManager.Application.Mcp;
using FinanceManager.Application.Users;
using FinanceManager.Application.WellKnown;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Admin;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Shared.WellKnown;
using FinanceManager.Web.Services.Updates;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the administrative settings tool <c>fm_admin_settings</c> (T38): reads
/// the admin-editable settings of the setup area (MCP kill switch/write-stop lid, update
/// settings, well-known endpoints) and changes a bounded subset of them — every <c>set</c>
/// call is final/effective and runs through the approval queue
/// (<c>Err_Mcp_ApprovalRequired</c> → <c>AiActionRequest</c>).
/// Admin-gated (the owner must hold the admin role, <c>Err_Mcp_AdminRequired</c> otherwise).
/// Scope: (Administration, Lesen); set additionally (Administration, Änderung) —
/// evaluated inside the handler.
/// </summary>
public sealed class FmAdminSettingsDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_admin_settings";

    /// <summary>Creates the descriptor.</summary>
    public FmAdminSettingsDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"operation\":{\"type\":\"string\",\"enum\":[\"get\",\"set\"],\"description\":\"Settings operation (default 'get'); set requires approval.\"},\"section\":{\"type\":\"string\",\"enum\":[\"mcp\",\"updates\",\"wellKnown\",\"all\"],\"description\":\"Settings section (default 'all' for get; required for set).\"},\"enabled\":{\"type\":\"boolean\",\"description\":\"Set: MCP endpoint enabled (section mcp) or automatic update checks enabled (section updates).\"},\"writeAccessEnabled\":{\"type\":\"boolean\",\"description\":\"Set: MCP write-stop lid (section mcp).\"},\"includePrereleases\":{\"type\":\"boolean\",\"description\":\"Set: prereleases considered during update checks (section updates).\"},\"changePasswordUrl\":{\"type\":\"string\",\"description\":\"Set: change-password well-known redirect target — local root path or absolute http/https URL (section wellKnown).\"},\"idempotencyKey\":{\"type\":\"string\",\"description\":\"Client deduplication key for the approval request.\"},\"reasonText\":{\"type\":\"string\",\"description\":\"Optional client reason text (untrusted).\"}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Reads the admin-editable settings of the setup area (default operation 'get'; " +
           "sections mcp = MCP kill switch/write-stop lid, updates = update settings, " +
           "wellKnown = well-known endpoints) or changes a bounded subset ('set': mcp.enabled, " +
           "mcp.writeAccessEnabled, updates.enabled, updates.includePrereleases, " +
           "wellKnown.changePasswordUrl — unspecified fields keep their values). Every set " +
           "call is final/effective and requires owner approval (AiActionRequest queue). " +
           "Restricted to owners with administrative privileges. Scope: (Administration, " +
           "Lesen); set additionally (Administration, Änderung).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Schreibvorbereitend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Administration, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_admin_settings</c>: adapts <see cref="IMcpSettingsService"/>,
/// <see cref="IUpdateOrchestrator"/> and <see cref="IWellKnownSettingsService"/> — the same
/// contracts the admin REST endpoints use. The set path merges the provided fields over a
/// fresh read inside the approved execution (bait-and-switch safe: only the declared field
/// subset may change). Free-text configuration values are untrusted content
/// (<c>untrusted_data</c>).
/// </summary>
public sealed class FmAdminSettingsHandler : IMcpToolHandler
{
    private const string EntityType = "AdminSettings";

    private readonly IUserAdminService _users;
    private readonly IMcpSettingsService _mcpSettings;
    private readonly IUpdateOrchestrator _updates;
    private readonly IWellKnownSettingsService _wellKnown;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;

    /// <summary>Creates the handler.</summary>
    public FmAdminSettingsHandler(
        IUserAdminService users,
        IMcpSettingsService mcpSettings,
        IUpdateOrchestrator updates,
        IWellKnownSettingsService wellKnown,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter)
    {
        _users = users;
        _mcpSettings = mcpSettings;
        _updates = updates;
        _wellKnown = wellKnown;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
    }

    /// <inheritdoc />
    public string ToolName => FmAdminSettingsDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        await McpAdminGuards.RequireAdminAsync(_users, owner, ct);

        var operation = McpToolArguments.OptionalString(arguments, "operation", 30) ?? "get";
        return operation switch
        {
            "get" => await GetAsync(arguments, ct),
            "set" => await SetAsync(arguments, context, ct),
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'operation' must be one of get|set.")
        };
    }

    // ------------------------------------------------------------------
    // get
    // ------------------------------------------------------------------

    private async Task<object> GetAsync(JsonElement arguments, CancellationToken ct)
    {
        var section = McpToolArguments.OptionalString(arguments, "section", 20) ?? "all";
        if (section is not ("mcp" or "updates" or "wellKnown" or "all"))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'section' must be one of mcp|updates|wellKnown|all.");
        }

        object? mcp = null;
        if (section is "mcp" or "all")
        {
            var s = await _mcpSettings.GetAsync(ct);
            mcp = new { enabled = s.Enabled, writeAccessEnabled = s.WriteAccessEnabled };
        }
        object? updates = null;
        if (section is "updates" or "all")
        {
            updates = FmAdminUpdatesHandler.MapSettings(await _updates.GetSettingsAsync(ct));
        }
        object? wellKnown = null;
        if (section is "wellKnown" or "all")
        {
            var s = await _wellKnown.GetAsync(ct);
            wellKnown = new { untrusted_data = new { changePasswordUrl = s.ChangePasswordUrl } };
        }
        return new { operation = "get", mcp, updates, wellKnown };
    }

    // ------------------------------------------------------------------
    // set — genehmigungspflichtig (final/effective settings change)
    // ------------------------------------------------------------------

    private async Task<object> SetAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var section = McpToolArguments.OptionalString(arguments, "section", 20)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'section' is required for set (mcp|updates|wellKnown).");
        if (section is not ("mcp" or "updates" or "wellKnown"))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'section' must be one of mcp|updates|wellKnown for set.");
        }

        // The bounded field subset: at least one writable field must be provided, and no
        // field outside the section's whitelist may be set.
        var enabled = McpToolArguments.OptionalBool(arguments, "enabled");
        var writeAccessEnabled = McpToolArguments.OptionalBool(arguments, "writeAccessEnabled");
        var includePrereleases = McpToolArguments.OptionalBool(arguments, "includePrereleases");
        var changePasswordUrl = McpToolArguments.OptionalString(arguments, "changePasswordUrl", 2048);
        ValidateSetArguments(section, enabled, writeAccessEnabled, includePrereleases, changePasswordUrl);

        return await McpAdminGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.Administration, McpRightKind.Modify,
            "set",
            $"Changes admin-editable settings of section '{section}' — effective immediately " +
            "(MCP kill switch/write-stop lid, update settings or well-known endpoints).",
            new { operation = "set", section, enabled, writeAccessEnabled, includePrereleases, changePasswordUrl }, ct,
            executeApproved: async ct2 =>
            {
                var staged = await McpWriteAudit.StageDataChangeAsync(
                    _auditWriter, context, "AdminSettingsChanged", ToolName,
                    new List<McpAuditFieldChange>
                    {
                        new(EntityType, Guid.Empty, "SectionChanged", null, section)
                    },
                    null,
                    JsonSerializer.Serialize(new { operation = "set", section }), ct2);
                try
                {
                    return section switch
                    {
                        "mcp" => await ApplyMcpAsync(enabled, writeAccessEnabled, ct2),
                        "updates" => await ApplyUpdatesAsync(enabled, includePrereleases, ct2),
                        _ => await ApplyWellKnownAsync(changePasswordUrl!, ct2)
                    };
                }
                catch
                {
                    McpWriteAudit.DiscardStaged(context, staged);
                    throw;
                }
            });
    }

    private static void ValidateSetArguments(string section,
        bool? enabled, bool? writeAccessEnabled, bool? includePrereleases, string? changePasswordUrl)
    {
        if (section is "mcp" or "updates" && changePasswordUrl is not null)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                $"Argument 'changePasswordUrl' is not writable on section '{section}'.");
        }
        if (section == "mcp" && includePrereleases.HasValue)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'includePrereleases' is not writable on section 'mcp'.");
        }
        if (section == "updates" && writeAccessEnabled.HasValue)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'writeAccessEnabled' is not writable on section 'updates'.");
        }
        if (section == "wellKnown"
            && (enabled.HasValue || writeAccessEnabled.HasValue || includePrereleases.HasValue))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Only 'changePasswordUrl' is writable on section 'wellKnown'.");
        }
        var provided = section switch
        {
            "mcp" => enabled.HasValue || writeAccessEnabled.HasValue,
            "updates" => enabled.HasValue || includePrereleases.HasValue,
            _ => changePasswordUrl is not null
        };
        if (!provided)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                $"Set on section '{section}' requires at least one writable field.");
        }
        if (section == "wellKnown" && !WellKnownUrlValidator.IsValidUrl(changePasswordUrl))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'changePasswordUrl' must be a local root path or an absolute http/https URL.");
        }
    }

    private async Task<object> ApplyMcpAsync(bool? enabled, bool? writeAccessEnabled, CancellationToken ct)
    {
        var current = await _mcpSettings.GetAsync(ct);
        var effEnabled = enabled ?? current.Enabled;
        var effWrite = writeAccessEnabled ?? current.WriteAccessEnabled;
        if (effEnabled == current.Enabled && effWrite == current.WriteAccessEnabled)
        {
            return new { operation = "set", section = "mcp", applied = false, reason = "unchanged" };
        }
        await _mcpSettings.UpdateAsync(new McpSettingsUpdateRequest(effEnabled, effWrite), ct);
        return new
        {
            operation = "set",
            section = "mcp",
            applied = true,
            mcp = new { enabled = effEnabled, writeAccessEnabled = effWrite }
        };
    }

    private async Task<object> ApplyUpdatesAsync(bool? enabled, bool? includePrereleases, CancellationToken ct)
    {
        var current = await _updates.GetSettingsAsync(ct);
        var effEnabled = enabled ?? current.Enabled;
        var effPrereleases = includePrereleases ?? current.IncludePrereleases;
        if (effEnabled == current.Enabled && effPrereleases == current.IncludePrereleases)
        {
            return new { operation = "set", section = "updates", applied = false, reason = "unchanged" };
        }
        var saved = await _updates.SaveSettingsAsync(new UpdateSettingsUpdateRequest(
            effEnabled,
            current.RepositoryOwner,
            current.RepositoryName,
            current.ManifestAssetName,
            current.SourceCheckStartTime,
            current.SourceCheckEndTime,
            current.ScheduledInstallTime,
            current.ServiceName,
            current.ExecutablePath,
            current.WorkingDirectory,
            current.HealthTimeoutSeconds,
            effPrereleases), ct);
        return new
        {
            operation = "set",
            section = "updates",
            applied = true,
            updates = FmAdminUpdatesHandler.MapSettings(saved)
        };
    }

    private async Task<object> ApplyWellKnownAsync(string changePasswordUrl, CancellationToken ct)
    {
        await _wellKnown.UpdateAsync(new WellKnownSettingsUpdateRequest(changePasswordUrl), ct);
        var saved = await _wellKnown.GetAsync(ct);
        return new
        {
            operation = "set",
            section = "wellKnown",
            applied = true,
            wellKnown = new { untrusted_data = new { changePasswordUrl = saved.ChangePasswordUrl } }
        };
    }
}

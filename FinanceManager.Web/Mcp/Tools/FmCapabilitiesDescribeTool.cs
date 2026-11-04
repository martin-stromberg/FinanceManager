using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the meta tool <c>fm_capabilities_describe</c> (T32): reports the effective
/// profile of the own access — level, scopes, filters, approval mode, catalog version.
/// Carries the closed meta-scope <c>Zugangsprofil</c> instead of a data tuple.
/// </summary>
public sealed class FmCapabilitiesDescribeDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_capabilities_describe";

    /// <summary>Creates the descriptor.</summary>
    public FmCapabilitiesDescribeDescriptor() : base("{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}") { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Returns the effective permission profile of this MCP access (level, scopes, filters, approval mode, catalog version). Meta-scope: Zugangsprofil.";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => Array.Empty<McpToolScope>();

    /// <inheritdoc />
    public override string? MetaScope => "Zugangsprofil";
}

/// <summary>
/// Handler of <c>fm_capabilities_describe</c>: reads only the own access configuration.
/// </summary>
public sealed class FmCapabilitiesDescribeHandler : IMcpToolHandler
{
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpToolRegistry _registry;
    private readonly IOptionsMonitor<McpOptions> _options;

    /// <summary>Creates the handler.</summary>
    public FmCapabilitiesDescribeHandler(IMcpScopeEvaluator scopeEvaluator, IMcpToolRegistry registry, IOptionsMonitor<McpOptions> options)
    {
        _scopeEvaluator = scopeEvaluator;
        _registry = registry;
        _options = options;
    }

    /// <inheritdoc />
    public string ToolName => FmCapabilitiesDescribeDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var access = context.Access.Access
            ?? throw new McpToolException("Err_Mcp_Unauthorized", "No MCP access context.");
        var scopes = await _scopeEvaluator.GetEffectiveScopesAsync(access, ct);
        return new
        {
            access = new
            {
                id = access.Id,
                name = access.Name,
                level = access.Level.ToString(),
                approvalMode = access.ApprovalMode.ToString(),
                status = access.Status.ToString(),
                expiresUtc = access.ExpiresUtc,
                isExpired = access.IsExpired(DateTime.UtcNow),
                allowedIpRanges = access.AllowedIpRanges,
                metaScope = "Zugangsprofil"
            },
            effectiveScopes = scopes.Select(s => new
            {
                dataType = s.DataType.ToString(),
                right = s.Right.ToString(),
                filters = new
                {
                    accountTypes = s.AccountTypesCsv,
                    contactCategoryIds = s.ContactCategoryIdsCsv,
                    securityCategoryIds = s.SecurityCategoryIdsCsv,
                    savingsPlanCategoryIds = s.SavingsPlanCategoryIdsCsv,
                    attachmentCategoryIds = s.AttachmentCategoryIdsCsv
                }
            }).ToList(),
            catalogVersion = _registry.CatalogVersion,
            writeAccessEnabled = _options.CurrentValue.WriteAccess.Enabled
        };
    }
}

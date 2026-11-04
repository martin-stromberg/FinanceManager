using System.Text.Json;
using FinanceManager.Domain.Mcp;

namespace FinanceManager.Application.Mcp;

/// <summary>
/// One scope requirement of a tool: the (Datentyp × Rechteart) tuple the effective profile
/// of the calling access must grant.
/// </summary>
/// <param name="DataType">Required data type.</param>
/// <param name="Right">Required right.</param>
public sealed record McpToolScope(McpDataType DataType, McpRightKind Right);

/// <summary>
/// Static descriptor of one MCP tool of the compiled catalog (<c>katalog-v&lt;n&gt;</c>).
/// "No tool without a scope" is enforced fail-fast at registry build: a descriptor either
/// declares <see cref="RequiredScopes"/> or carries the closed <see cref="MetaScope"/>
/// marker of the meta tools T32/T33 (scope-und-toolkatalog.md §3.6).
/// </summary>
public interface IMcpToolDescriptor
{
    /// <summary>Stable tool name in <c>fm_&lt;bereich&gt;_&lt;aktion&gt;</c> snake_case.</summary>
    string Name { get; }

    /// <summary>
    /// Tool description for the client listing: purpose, scope requirement and deprecation.
    /// Never interpolated with user data (untrusted-content rule).
    /// </summary>
    string Description { get; }

    /// <summary>Security class driving approval treatment and the rate-limit tier.</summary>
    McpToolSecurityClass SecurityClass { get; }

    /// <summary>
    /// Declared scope tuples the call requires; all tuples must be granted (AND).
    /// Empty when <see cref="MetaScope"/> is set.
    /// </summary>
    IReadOnlyList<McpToolScope> RequiredScopes { get; }

    /// <summary>
    /// Meta-scope name of meta tools (<c>Zugangsprofil</c>, <c>Ausführungsstatus</c>) or
    /// <see langword="null"/> for data tools. The meta scope is inherent to every access and
    /// is checked through the ownership check only.
    /// </summary>
    string? MetaScope { get; }

    /// <summary>JSON input schema (whitelist parameters, no free-text commands).</summary>
    JsonDocument InputSchema { get; }

    /// <summary>Whether the tool is deprecated (announced in the listing, still callable).</summary>
    bool IsDeprecated { get; }
}

/// <summary>
/// Static registry of the compiled MCP tool catalog <c>katalog-v1</c>. The registry is code,
/// not configuration: descriptors are contributed via DI and the catalog version is a
/// constant reported by <c>fm_capabilities_describe</c> and the audit entries.
/// </summary>
public interface IMcpToolRegistry
{
    /// <summary>Active catalog version (<c>katalog-v1</c>).</summary>
    string CatalogVersion { get; }

    /// <summary>All registered tool descriptors (client-independent full listing).</summary>
    IReadOnlyList<IMcpToolDescriptor> Tools { get; }

    /// <summary>
    /// Finds a tool descriptor by name.
    /// </summary>
    /// <param name="name">The <c>fm_*</c> tool name.</param>
    /// <returns>The descriptor or <see langword="null"/> when unknown.</returns>
    IMcpToolDescriptor? Find(string name);
}

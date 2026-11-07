using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using System.Text.Json;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Base class for compiled tool descriptors; every concrete descriptor declares
/// <see cref="IMcpToolDescriptor.RequiredScopes"/> or <see cref="IMcpToolDescriptor.MetaScope"/>.
/// "No tool without a scope" is enforced fail-fast by <see cref="McpToolRegistry"/>.
/// </summary>
public abstract class McpToolDescriptorBase : IMcpToolDescriptor
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract McpToolSecurityClass SecurityClass { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<McpToolScope> RequiredScopes { get; }

    /// <inheritdoc />
    public virtual string? MetaScope => null;

    /// <inheritdoc />
    public virtual bool IsDeprecated => false;

    /// <inheritdoc />
    public virtual string? SinceVersion => McpToolRegistry.Version;

    /// <inheritdoc />
    public virtual string? ReplacedBy => null;

    /// <inheritdoc />
    public JsonDocument InputSchema => _schema.Value;
    private readonly Lazy<JsonDocument> _schema;

    /// <summary>Initializes the descriptor with its JSON input schema text.</summary>
    /// <param name="inputSchemaJson">JSON schema text of the tool arguments.</param>
    protected McpToolDescriptorBase(string inputSchemaJson)
    {
        _schema = new Lazy<JsonDocument>(() => JsonDocument.Parse(inputSchemaJson));
    }
}

/// <summary>
/// Compiled MCP tool registry (<c>katalog-v1</c>): collects all registered
/// <see cref="IMcpToolDescriptor"/> services. Descriptors and handlers live in DI.
/// </summary>
public sealed class McpToolRegistry : IMcpToolRegistry
{
    /// <summary>Active catalog version constant.</summary>
    public const string Version = "katalog-v1";

    private readonly IReadOnlyList<IMcpToolDescriptor> _tools;
    private readonly Dictionary<string, IMcpToolDescriptor> _byName;

    /// <summary>
    /// Creates the registry from all registered descriptors. Fails fast on an invalid
    /// descriptor: every tool must declare either at least one
    /// <see cref="IMcpToolDescriptor.RequiredScopes"/> tuple or a non-empty
    /// <see cref="IMcpToolDescriptor.MetaScope"/> — exactly one of the two, because the
    /// dispatcher skips the tuple check once a meta scope is present.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A descriptor declares neither scopes nor a meta scope ("no tool without a scope")
    /// or declares both (the scope tuples would be silently ignored).
    /// </exception>
    public McpToolRegistry(IEnumerable<IMcpToolDescriptor> tools)
    {
        _tools = tools.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
        foreach (var tool in _tools)
        {
            var hasScopes = tool.RequiredScopes.Count > 0;
            var hasMetaScope = !string.IsNullOrWhiteSpace(tool.MetaScope);
            if (hasScopes == hasMetaScope)
            {
                throw new InvalidOperationException(
                    $"Invalid MCP tool descriptor '{tool.Name}': declare either at least one RequiredScopes tuple or a MetaScope, exactly one of the two.");
            }
        }
        _byName = _tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public string CatalogVersion => Version;

    /// <inheritdoc />
    public IReadOnlyList<IMcpToolDescriptor> Tools => _tools;

    /// <inheritdoc />
    public IMcpToolDescriptor? Find(string name)
        => name is not null && _byName.TryGetValue(name, out var d) ? d : null;
}

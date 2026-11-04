using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using System.Text.Json;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Base class for compiled tool descriptors; "no tool without scope" is enforced because
/// every concrete descriptor must declare <see cref="IMcpToolDescriptor.RequiredScopes"/> or
/// <see cref="IMcpToolDescriptor.MetaScope"/>.
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

    /// <summary>Creates the registry from all registered descriptors.</summary>
    public McpToolRegistry(IEnumerable<IMcpToolDescriptor> tools)
    {
        _tools = tools.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
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

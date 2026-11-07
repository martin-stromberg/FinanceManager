namespace FinanceManager.Domain.Mcp;

/// <summary>
/// Database-stored, administrator-editable settings of the MCP integration — the global
/// kill switch (<see cref="Enabled"/>) and the separate write-stop lid
/// (<see cref="WriteAccessEnabled"/>). Both default to <see langword="false"/> (opt-in:
/// MCP ships disabled, and write access is an additional deliberate activation step).
/// All other <c>Mcp:*</c> configuration (rate limits, retention, SSE, anomaly, token hash,
/// approval) remains appsettings-bound via <c>McpOptions</c>.
/// </summary>
public sealed class McpSettings : Entity, IAggregateRoot
{
    /// <summary>
    /// Fixed identifier of the single settings row. Enforcing one well-known key guarantees that the
    /// table never holds more than one row — a concurrent first insert collides on the primary key
    /// instead of creating a duplicate.
    /// </summary>
    /// <value>The fixed <see cref="Guid"/> of the singleton settings row.</value>
    public static readonly Guid SingletonId = new("3c9a1e2f-7b4d-4a5c-8e6f-0d1b2c3a4e5f");

    private McpSettings()
    {
    }

    /// <summary>
    /// Creates the default settings row used before the admin has configured the switches.
    /// The instance carries <see cref="SingletonId"/> so it represents the one and only settings row.
    /// Both switches default to <see langword="false"/> — the safe off state.
    /// </summary>
    /// <returns>A default settings instance.</returns>
    public static McpSettings CreateDefault()
    {
        return new McpSettings
        {
            Id = SingletonId,
            Enabled = false,
            WriteAccessEnabled = false
        };
    }

    /// <summary>
    /// Global kill switch. When <see langword="false"/> the <c>/mcp</c> endpoint rejects all
    /// requests with HTTP 503 before any authentication work.
    /// </summary>
    public bool Enabled { get; private set; }

    /// <summary>
    /// Write-stop lid: when <see langword="false"/> all tools of the classes
    /// <see cref="McpToolSecurityClass.Schreibvorbereitend"/> and
    /// <see cref="McpToolSecurityClass.Genehmigungspflichtig"/> are rejected while read,
    /// suggest and meta tools keep working. Defaults to <see langword="false"/> — write
    /// access is an explicit opt-in independent of <see cref="Enabled"/>.
    /// </summary>
    public bool WriteAccessEnabled { get; private set; }

    /// <summary>Updates both switches.</summary>
    /// <param name="enabled">Whether the MCP endpoint accepts requests.</param>
    /// <param name="writeAccessEnabled">Whether write-class tools may run.</param>
    public void Update(bool enabled, bool writeAccessEnabled)
    {
        Enabled = enabled;
        WriteAccessEnabled = writeAccessEnabled;
        Touch();
    }
}

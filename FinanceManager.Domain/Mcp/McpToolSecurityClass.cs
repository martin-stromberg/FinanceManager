namespace FinanceManager.Domain.Mcp;

/// <summary>
/// Security classification of an MCP tool (scope-und-toolkatalog.md §2). Determines the
/// approval treatment and the rate-limit tier of a tool. Every tool belongs to exactly
/// one class; the class follows the highest right kind the tool exercises.
/// </summary>
public enum McpToolSecurityClass
{
    /// <summary>
    /// No write effect; outputs records, aggregates or documents. Direct execution.
    /// </summary>
    Lesend = 0,

    /// <summary>
    /// Produces domain suggestions without persisting user data. Direct execution.
    /// </summary>
    Vorschlagend = 1,

    /// <summary>
    /// Mutates non-final data (draft fields, master data, configurations, attachments).
    /// Executed directly or via an approval request depending on the access approval mode.
    /// </summary>
    Schreibvorbereitend = 2,

    /// <summary>
    /// Produces final/irreversible effects (booking, reversal, deletion/archival, destructive
    /// merge). Always executed via an approval request unless durably pre-approved.
    /// </summary>
    Genehmigungspflichtig = 3
}

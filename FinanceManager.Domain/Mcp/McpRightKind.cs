namespace FinanceManager.Domain.Mcp;

/// <summary>
/// Right kind (Rechteart) of an MCP scope tuple. Each right bundles a defined set of
/// business actions on a data type (berechtigungsmodell.md §7).
/// </summary>
/// <remarks>
/// Persisted via <c>HasConversion&lt;short&gt;()</c>. Value list is normative
/// (datenmodell-erweiterungen.md §2.2) and must not be changed without updating the concept.
/// </remarks>
public enum McpRightKind
{
    /// <summary>Reading/listing/exporting data of the type.</summary>
    Read = 0,

    /// <summary>Producing suggestions without persisting them.</summary>
    Suggest = 1,

    /// <summary>Completing draft data (e.g. statement draft entry enrichment).</summary>
    Complete = 2,

    /// <summary>Modifying existing records.</summary>
    Modify = 3,

    /// <summary>Creating new records via the regular domain paths.</summary>
    Create = 4,

    /// <summary>Booking/committing drafts (postings, imports, side effects).</summary>
    Book = 5,

    /// <summary>Deleting records including reversals.</summary>
    Delete = 6
}

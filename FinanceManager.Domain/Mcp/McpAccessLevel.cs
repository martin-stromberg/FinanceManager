namespace FinanceManager.Domain.Mcp;

/// <summary>
/// Access level (Freigabestufe) of an MCP access. Determines the default scope profile that is
/// derived server-side at runtime; only <see cref="Individuell"/> persists explicit
/// <see cref="McpAccessScope"/> rows.
/// </summary>
/// <remarks>
/// Persisted via <c>HasConversion&lt;short&gt;()</c>. Value list is normative
/// (datenmodell-erweiterungen.md §2.2) and must not be changed without updating the concept.
/// </remarks>
public enum McpAccessLevel
{
    /// <summary>
    /// Read-only analysis access on all data types.
    /// </summary>
    Analyse = 0,

    /// <summary>
    /// Read plus suggestion access on all data types.
    /// </summary>
    Assistent = 1,

    /// <summary>
    /// Read, suggestion and limited completion/modify/create access on statement drafts and
    /// supporting master data (approval required per access settings).
    /// </summary>
    Sachbearbeiter = 2,

    /// <summary>
    /// Read, suggestion, completion, modify and create access on all applicable data types;
    /// booking and deletion only with approval or as pre-approved scope.
    /// </summary>
    Buchhalter = 3,

    /// <summary>
    /// Individually configured scope profile persisted as <see cref="McpAccessScope"/> rows.
    /// </summary>
    Individuell = 4
}

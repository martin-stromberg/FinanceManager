using FinanceManager.Domain.Mcp;

namespace FinanceManager.Application.Mcp;

/// <summary>
/// Verdict of a record-level scope evaluation.
/// </summary>
public enum McpAccessVerdict
{
    /// <summary>The call may proceed on this record.</summary>
    Allowed = 0,

    /// <summary>A scope/filter/lid rule denies access (defined error on tool level).</summary>
    Denied = 1,

    /// <summary>
    /// The record is not visible to the access owner (stage-0 ownership failed) —
    /// it behaves like a non-existing record (404 semantics, no information leak).
    /// </summary>
    NotFound = 2
}

/// <summary>
/// Record context for the record-level (datensatzscharf) scope evaluation
/// (berechtigungsmodell.md §6 Ableitungstabelle). Adapters supply the anchor references and
/// classification values they already know from the loaded record; the evaluator resolves
/// ownership, share lids and filters against the database.
/// </summary>
public sealed record McpRecordContext
{
    /// <summary>Direct owner of the record, when the entity carries an OwnerUserId.</summary>
    public Guid? OwnerUserId { get; init; }

    /// <summary>Account anchor (Posting kind Bank, StatementImport, AccountLinkedIban, detected draft account).</summary>
    public Guid? AccountId { get; init; }

    /// <summary>Contact anchor (Posting kind Contact, AliasName, linked draft entry contact).</summary>
    public Guid? ContactId { get; init; }

    /// <summary>Savings plan anchor (Posting kind SavingsPlan).</summary>
    public Guid? SavingsPlanId { get; init; }

    /// <summary>Security anchor (Posting kind Security, SecurityPrice).</summary>
    public Guid? SecurityId { get; init; }

    /// <summary>Statement import anchor (StatementEntry → import → account).</summary>
    public Guid? StatementImportId { get; init; }

    /// <summary>Statement draft anchor (StatementDraftEntry → draft).</summary>
    public Guid? DraftId { get; init; }

    /// <summary>Account type value (short) of the relevant account, for the AccountTypes filter dimension.</summary>
    public short? AccountTypeValue { get; init; }

    /// <summary>Contact category id of the record (filter dimension Kontaktgruppen).</summary>
    public Guid? ContactCategoryId { get; init; }

    /// <summary>Security category id of the record (filter dimension Wertpapierkategorien).</summary>
    public Guid? SecurityCategoryId { get; init; }

    /// <summary>Savings plan category id of the record (filter dimension Sparplankategorien).</summary>
    public Guid? SavingsPlanCategoryId { get; init; }

    /// <summary>Attachment category id of the record (filter dimension Anhangkategorien).</summary>
    public Guid? AttachmentCategoryId { get; init; }
}

/// <summary>
/// Evaluates the MCP permission model stages 0–3 (berechtigungsmodell.md §8): effective scope
/// profile resolution (level profile vs. persisted <see cref="McpAccessScope"/> rows), tuple
/// checks, filter-CSV evaluation (AND over dimensions, OR over scopes, unclassified records
/// not matched), the derivation table for indirectly owned entities and the
/// <c>AccountShare</c> role lid. The evaluation is resolved at runtime for every call —
/// profile changes take effect from the next call.
/// </summary>
public interface IMcpScopeEvaluator
{
    /// <summary>
    /// Resolves the effective scope profile of an access: the level default profile for
    /// standard levels (no persisted rows) or the persisted <see cref="McpAccessScope"/> rows
    /// for <see cref="McpAccessLevel.Individuell"/>.
    /// </summary>
    /// <param name="access">The MCP access.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The effective scope rows (filter-less synthetic rows for standard levels).</returns>
    Task<IReadOnlyList<McpAccessScope>> GetEffectiveScopesAsync(McpAccess access, CancellationToken ct);

    /// <summary>
    /// Stage-1 check: whether the effective profile of the access contains the scope tuple
    /// (data type × right). Record-level filters and lids are not part of this check.
    /// </summary>
    /// <param name="access">The MCP access.</param>
    /// <param name="dataType">The required data type.</param>
    /// <param name="right">The required right.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the tuple is granted.</returns>
    Task<bool> IsAllowedAsync(McpAccess access, McpDataType dataType, McpRightKind right, CancellationToken ct);

    /// <summary>
    /// Record-level check for adapters (stages 0–3): ownership/share resolution via the
    /// derivation table, share-role lid, scope tuple and per-record filter match.
    /// </summary>
    /// <param name="access">The MCP access.</param>
    /// <param name="dataType">The data type of the record.</param>
    /// <param name="right">The exercised right.</param>
    /// <param name="record">The record context with anchors and classification values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The evaluation verdict.</returns>
    Task<McpAccessVerdict> CheckRecordAccessAsync(McpAccess access, McpDataType dataType, McpRightKind right, McpRecordContext record, CancellationToken ct);
}

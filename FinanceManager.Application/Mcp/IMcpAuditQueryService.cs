using FinanceManager.Shared.Dtos.Mcp;

namespace FinanceManager.Application.Mcp;

/// <summary>
/// Filter of an audit timeline query (audit-konzept.md §6.1, F-0601). All fields are
/// optional and combined with AND semantics.
/// </summary>
/// <param name="FromUtc">Optional inclusive lower bound of <c>Utc</c>.</param>
/// <param name="ToUtc">Optional inclusive upper bound of <c>Utc</c>.</param>
/// <param name="Category">Optional category (McpAuditCategory as short).</param>
/// <param name="McpAccessId">Optional restriction to one access (incl. deleted — the audit
/// keeps the soft reference).</param>
/// <param name="CorrelationId">Optional correlation id of an effect chain.</param>
/// <param name="ToolName">Optional exact <c>fm_*</c> tool name.</param>
/// <param name="ResultStatus">Optional exact result code (<c>Success</c>, <c>Denied</c>, …).</param>
public sealed record McpAuditQueryFilter(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    short? Category = null,
    Guid? McpAccessId = null,
    Guid? CorrelationId = null,
    string? ToolName = null,
    string? ResultStatus = null);

/// <summary>
/// Read path of the MCP audit trail (F-0601, audit-konzept.md §6, architekturkonzept.md §6/§8):
/// owner-isolated queries over <c>McpAuditEntries</c> incl. field deltas, hash-chain
/// verification (delegated to the writer path) and the per-access usage aggregation (F-0603).
/// Every method is strictly scoped to the passed owner user id — foreign rows are
/// invisible and foreign ids behave like non-existing records.
/// </summary>
/// <remarks>
/// The audit trail is append-only by convention: this service exposes only reads. Export
/// data is delivered as serialized DTOs — never binary payloads (audit-konzept.md §4.2).
/// </remarks>
public interface IMcpAuditQueryService
{
    /// <summary>
    /// Lists the audit entries of an owner ordered newest first (paging, filters).
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="filter">Optional AND-combined filters.</param>
    /// <param name="skip">Items to skip (≥ 0).</param>
    /// <param name="take">Items to take (server-clamped ≤ 200).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching list items (no payloads, <c>ChangeCount</c> only).</returns>
    Task<IReadOnlyList<McpAuditEntryListItemDto>> ListAsync(
        Guid ownerUserId, McpAuditQueryFilter filter, int skip, int take, CancellationToken ct);

    /// <summary>
    /// Gets one audit entry of the owner including payload and field deltas.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="id">Entry id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The detail DTO or <see langword="null"/> when not found/not owned.</returns>
    Task<McpAuditEntryDetailDto?> GetAsync(Guid ownerUserId, Guid id, CancellationToken ct);

    /// <summary>
    /// Verifies the integrity of the owner's audit chain (gap-free monotonic
    /// <c>SequenceNo</c>, intact <c>PrevHash</c> linkage, valid recomputed <c>EntryHash</c>).
    /// Delegates to the hash-chained write path (<see cref="IMcpAuditWriter.VerifyAsync"/>).
    /// </summary>
    /// <param name="ownerUserId">Owner user id whose chain is verified.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The verification result (intact flag, first breach, sequence gaps).</returns>
    Task<McpAuditVerifyResultDto> VerifyAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Loads the filtered entries of the owner for export (full detail rows incl. field
    /// deltas), ordered chronologically and capped at <paramref name="maxRows"/>.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="filter">Optional AND-combined filters (same semantics as <see cref="ListAsync"/>).</param>
    /// <param name="maxRows">Hard row cap of the export.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The export rows (serialized data, never binary payloads).</returns>
    Task<IReadOnlyList<McpAuditEntryDetailDto>> ExportAsync(
        Guid ownerUserId, McpAuditQueryFilter filter, int maxRows, CancellationToken ct);

    /// <summary>
    /// Aggregates the usage overview of one access of the owner (F-0603): call counters,
    /// failure rate, top tools and the most recent calls — all derived from the audit trail.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="mcpAccessId">Access id (may be revoked/deleted — the soft reference stays).</param>
    /// <param name="recentTake">Number of recent call entries to include (server-clamped).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The aggregated usage DTO, or <see langword="null"/> when the access id does not
    /// belong to the owner (404 semantics).
    /// </returns>
    Task<McpAccessUsageDto?> GetAccessUsageAsync(
        Guid ownerUserId, Guid mcpAccessId, int recentTake, CancellationToken ct);
}

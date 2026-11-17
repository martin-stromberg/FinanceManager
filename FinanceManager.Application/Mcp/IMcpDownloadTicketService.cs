namespace FinanceManager.Application.Mcp;

/// <summary>
/// Issued short-lived backup download ticket. The <see cref="Token"/> is the only
/// credential representation — it is an opaque, tamper-proof (signed/encrypted) string
/// embedded in the one-shot download URL; nothing about it is persisted.
/// </summary>
/// <param name="Token">The URL-safe ticket token.</param>
/// <param name="ExpiresUtc">Instant after which the ticket is rejected.</param>
public sealed record McpDownloadTicket(string Token, DateTime ExpiresUtc);

/// <summary>Validation outcome of a presented download ticket token.</summary>
public enum McpDownloadTicketValidationResult
{
    /// <summary>Ticket authentic and inside its lifetime.</summary>
    Valid,

    /// <summary>Ticket authentic but past its expiry instant.</summary>
    Expired,

    /// <summary>Ticket unreadable, malformed or not authentic.</summary>
    Invalid
}

/// <summary>
/// The bound grant recovered from an authentic ticket: the download is scoped to exactly
/// this owner/backup pair, independent of any request-side identity.
/// </summary>
/// <param name="OwnerUserId">Owner the ticket was issued for.</param>
/// <param name="BackupId">Backup record the ticket was issued for.</param>
/// <param name="McpAccessId">Causing MCP access (audit attribution), when known.</param>
/// <param name="ExpiresUtc">Expiry instant carried by the ticket.</param>
public sealed record McpDownloadTicketGrant(Guid OwnerUserId, Guid BackupId, Guid? McpAccessId, DateTime ExpiresUtc);

/// <summary>Result of a ticket validation.</summary>
/// <param name="Result">The outcome.</param>
/// <param name="Grant">The recovered grant — present for <see cref="McpDownloadTicketValidationResult.Valid"/> and <see cref="McpDownloadTicketValidationResult.Expired"/> (for audit attribution), <see langword="null"/> for invalid tokens.</param>
public sealed record McpDownloadTicketValidation(McpDownloadTicketValidationResult Result, McpDownloadTicketGrant? Grant);

/// <summary>
/// Issues and validates the short-lived, single-payload download tickets behind
/// <c>GET /mcp/download/{token}</c>. Tickets are stateless (encrypted/signed payload, no
/// database record): the token binds owner, backup and expiry, so no ticket store,
/// migration or cleanup job is required. The trade-off — no single-use consumption —
/// is accepted for a read-only artifact guarded by a ≤10-minute lifetime.
/// </summary>
public interface IMcpDownloadTicketService
{
    /// <summary>
    /// Issues a new ticket binding <paramref name="ownerUserId"/> + <paramref name="backupId"/>.
    /// </summary>
    /// <param name="ownerUserId">Owner the ticket grants the download for.</param>
    /// <param name="backupId">Backup record the ticket grants.</param>
    /// <param name="mcpAccessId">Optional causing MCP access (audit attribution only).</param>
    /// <param name="lifetime">Optional custom lifetime; defaults to the service's short TTL.</param>
    /// <returns>The issued ticket (token + expiry).</returns>
    McpDownloadTicket Issue(Guid ownerUserId, Guid backupId, Guid? mcpAccessId, TimeSpan? lifetime = null);

    /// <summary>
    /// Validates a presented token: authenticity first, expiry second. Invalid tokens
    /// yield no grant (uniform rejection, no detail leak).
    /// </summary>
    /// <param name="token">The raw token from the URL path.</param>
    /// <returns>The validation outcome plus the recovered grant when authentic.</returns>
    McpDownloadTicketValidation Validate(string token);
}

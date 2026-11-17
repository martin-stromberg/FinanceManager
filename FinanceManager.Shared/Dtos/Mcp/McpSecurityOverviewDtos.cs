namespace FinanceManager.Shared.Dtos.Mcp;

/// <summary>
/// Admin security overview of the MCP integration (F-1005; ui-konzept.md §5.2,
/// audit-konzept.md §6.2): instance-wide security metadata <b>without</b> any business
/// payload — the administrator sees access metadata, pending counters, the global switch
/// state and security events of all users, but never postings, snapshots or audit payloads
/// of foreign users.
/// </summary>
/// <param name="McpEnabled">Current state of the global <c>Mcp:Enabled</c> switch.</param>
/// <param name="WriteAccessEnabled">Current state of the <c>Mcp:WriteAccess:Enabled</c> write-stop lid.</param>
/// <param name="Aggregates">Instance-wide counters (accesses, requests, denials).</param>
/// <param name="Accesses">Access metadata of every owner (no secrets, no business data).</param>
/// <param name="RecentSecurityEvents">Newest security-category audit events (metadata only — no payloads).</param>
public sealed record McpSecurityOverviewDto(
    bool McpEnabled,
    bool WriteAccessEnabled,
    McpSecurityAggregatesDto Aggregates,
    IReadOnlyList<McpSecurityAccessDto> Accesses,
    IReadOnlyList<McpSecurityEventDto> RecentSecurityEvents);

/// <summary>
/// Instance-wide aggregate counters of the admin security overview.
/// </summary>
/// <param name="TotalAccesses">All configured accesses (every owner).</param>
/// <param name="ActiveAccesses">Currently usable accesses (active, not expired).</param>
/// <param name="PendingRequests">Approval requests awaiting a decision (all owners).</param>
/// <param name="ApprovedQueued">Approved requests queued for execution (all owners).</param>
/// <param name="DeniedCallsLast24h">Scope/gate denied calls of the last 24 hours.</param>
/// <param name="RateLimitedCallsLast24h">Rate-limited calls of the last 24 hours.</param>
/// <param name="FailedCallsLast24h">Failed tool calls of the last 24 hours.</param>
public sealed record McpSecurityAggregatesDto(
    int TotalAccesses,
    int ActiveAccesses,
    int PendingRequests,
    int ApprovedQueued,
    int DeniedCallsLast24h,
    int RateLimitedCallsLast24h,
    int FailedCallsLast24h);

/// <summary>
/// Access metadata row of the admin security overview — deliberately no token material,
/// no scope tuples and no owner business data; <see cref="OwnerUserId"/> is the opaque
/// owner reference (the admin knows user ids from user administration anyway).
/// </summary>
/// <param name="AccessId">Access identifier.</param>
/// <param name="OwnerUserId">Owning user id (opaque metadata reference).</param>
/// <param name="Name">Access name (metadata, not business data).</param>
/// <param name="Level">Freigabestufe (McpAccessLevel as short).</param>
/// <param name="Status">Lifecycle status (McpAccessStatus as short).</param>
/// <param name="ExpiresUtc">Optional token expiry.</param>
/// <param name="LastUsedUtc">Last successful authenticated call, or <see langword="null"/>.</param>
/// <param name="PendingRequests">Open approval requests of this access.</param>
public sealed record McpSecurityAccessDto(
    Guid AccessId,
    Guid OwnerUserId,
    string Name,
    short Level,
    short Status,
    DateTime? ExpiresUtc,
    DateTime? LastUsedUtc,
    int PendingRequests);

/// <summary>
/// Metadata of one security-category audit event (no payload, no business content —
/// audit-konzept.md §6.2).
/// </summary>
/// <param name="Utc">Event timestamp.</param>
/// <param name="EventType">Stable event type (e.g. <c>AuthFailed</c>, <c>RateLimited</c>).</param>
/// <param name="ResultStatus">Result code of the event.</param>
/// <param name="OwnerUserId">Affected owner (opaque metadata reference), or <see langword="null"/>.</param>
/// <param name="McpAccessName">Access name snapshot, or <see langword="null"/>.</param>
/// <param name="SourceIp">Recorded client IP of the event (IP-Sperrung diagnostics), or <see langword="null"/>.</param>
public sealed record McpSecurityEventDto(
    DateTime Utc,
    string EventType,
    string ResultStatus,
    Guid? OwnerUserId,
    string? McpAccessName,
    string? SourceIp);

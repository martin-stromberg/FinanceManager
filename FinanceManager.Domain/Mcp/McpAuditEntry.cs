namespace FinanceManager.Domain.Mcp;

/// <summary>
/// One tamper-evident audit entry (revisionssicherer Ereigniseintrag) of the MCP integration.
/// Entries are append-only: the application never updates or deletes them (the retention job is
/// the only deletion path). Each entry carries the chain anchor fields <see cref="SequenceNo"/>,
/// <see cref="PrevHash"/> and <see cref="EntryHash"/> per owner chain (global events form the
/// chain with <see cref="OwnerUserId"/> = <see langword="null"/>).
/// </summary>
public sealed class McpAuditEntry : Entity, IAggregateRoot
{
    /// <summary>
    /// Parameterless constructor for persistence/ORM and deserialization.
    /// </summary>
    private McpAuditEntry() { }

    /// <summary>
    /// Creates a new audit entry. The caller supplies the already computed chain values
    /// (<paramref name="sequenceNo"/>, <paramref name="prevHash"/>, <paramref name="entryHash"/>).
    /// </summary>
    /// <param name="utc">Server-side timestamp of the event.</param>
    /// <param name="sequenceNo">Monotonic sequence number of the chain.</param>
    /// <param name="category">Event category.</param>
    /// <param name="eventType">Stable event type code (e.g. <c>ToolCall</c>, <c>TokenRotated</c>).</param>
    /// <param name="resultStatus">Stable result code (e.g. <c>Success</c>, <c>Denied</c>).</param>
    /// <param name="entryHash">Hash over the canonicalized entry content (chain anchor). Required.</param>
    /// <param name="ownerUserId">Affected owner, or <see langword="null"/> for global events.</param>
    /// <param name="mcpAccessId">Optional soft reference to the causing access.</param>
    /// <param name="mcpAccessName">Optional snapshot of the access name.</param>
    /// <param name="toolName">Optional <c>fm_*</c> tool name for tool calls.</param>
    /// <param name="catalogVersion">Optional tool catalog version (<c>katalog-v&lt;n&gt;</c>).</param>
    /// <param name="context">Optional administrative context (<c>Ui</c>, <c>McpEndpoint</c>, <c>System/Job</c>, <c>Admin</c>).</param>
    /// <param name="correlationId">Optional correlation id linking call, request and entries.</param>
    /// <param name="sourceIp">Optional source IP of client calls.</param>
    /// <param name="durationMs">Optional tool call duration in milliseconds.</param>
    /// <param name="affectedCount">Optional number of affected records.</param>
    /// <param name="payloadJson">Optional category-specific payload (never token material or binary content).</param>
    /// <param name="relatedRequestId">Optional link to an approval/action request.</param>
    /// <param name="prevHash">Hash of the canonicalized predecessor entry of the same chain.</param>
    /// <exception cref="ArgumentException">Thrown when a required string argument is null or whitespace.</exception>
    public McpAuditEntry(DateTime utc,
        long sequenceNo,
        McpAuditCategory category,
        string eventType,
        string resultStatus,
        string entryHash,
        Guid? ownerUserId = null,
        Guid? mcpAccessId = null,
        string? mcpAccessName = null,
        string? toolName = null,
        string? catalogVersion = null,
        string? context = null,
        Guid? correlationId = null,
        string? sourceIp = null,
        int? durationMs = null,
        int? affectedCount = null,
        string? payloadJson = null,
        Guid? relatedRequestId = null,
        string? prevHash = null)
    {
        Utc = utc;
        SequenceNo = sequenceNo;
        Category = category;
        EventType = Guards.NotNullOrWhiteSpace(eventType, nameof(eventType));
        ResultStatus = Guards.NotNullOrWhiteSpace(resultStatus, nameof(resultStatus));
        EntryHash = Guards.NotNullOrWhiteSpace(entryHash, nameof(entryHash));
        OwnerUserId = ownerUserId;
        McpAccessId = mcpAccessId;
        McpAccessName = mcpAccessName;
        ToolName = toolName;
        CatalogVersion = catalogVersion;
        Context = context;
        CorrelationId = correlationId;
        SourceIp = sourceIp;
        DurationMs = durationMs;
        AffectedCount = affectedCount;
        PayloadJson = payloadJson;
        RelatedRequestId = relatedRequestId;
        PrevHash = prevHash;
    }

    /// <summary>
    /// Server-side timestamp of the event (always <c>UtcNow</c> of the server, never client time).
    /// </summary>
    public DateTime Utc { get; private set; }

    /// <summary>
    /// Monotonic sequence number of the entry within its chain (per owner resp. the global chain).
    /// </summary>
    public long SequenceNo { get; private set; }

    /// <summary>
    /// Event category.
    /// </summary>
    public McpAuditCategory Category { get; private set; }

    /// <summary>
    /// Stable event type code (e.g. <c>ToolCall</c>, <c>ScopeDenied</c>, <c>TokenRotated</c>,
    /// <c>ApprovalDecided</c>, <c>SwitchToggled</c>, <c>AuthFailed</c>, <c>RetentionPurged</c>).
    /// </summary>
    public string EventType { get; private set; } = null!;

    /// <summary>
    /// Affected owner; <see langword="null"/> for purely global events.
    /// </summary>
    public Guid? OwnerUserId { get; private set; }

    /// <summary>
    /// Optional soft reference to the causing MCP access (survives access deletion).
    /// </summary>
    public Guid? McpAccessId { get; private set; }

    /// <summary>
    /// Optional snapshot of the access name at event time.
    /// </summary>
    public string? McpAccessName { get; private set; }

    /// <summary>
    /// Optional <c>fm_*</c> tool name for tool calls.
    /// </summary>
    public string? ToolName { get; private set; }

    /// <summary>
    /// Optional tool catalog version (<c>katalog-v&lt;n&gt;</c>) at call time.
    /// </summary>
    public string? CatalogVersion { get; private set; }

    /// <summary>
    /// Optional administrative context (<c>Ui</c>, <c>McpEndpoint</c>, <c>System/Job</c>, <c>Admin</c>).
    /// </summary>
    public string? Context { get; private set; }

    /// <summary>
    /// Stable result code (<c>Success</c>, <c>Denied</c>, <c>Failed</c>, <c>RateLimited</c>,
    /// <c>RedirectedToApproval</c>, …).
    /// </summary>
    public string ResultStatus { get; private set; } = null!;

    /// <summary>
    /// Optional correlation id linking the tool call, approval request and audit entries.
    /// </summary>
    public Guid? CorrelationId { get; private set; }

    /// <summary>
    /// Optional source IP of client calls.
    /// </summary>
    public string? SourceIp { get; private set; }

    /// <summary>
    /// Optional tool call duration in milliseconds.
    /// </summary>
    public int? DurationMs { get; private set; }

    /// <summary>
    /// Optional number of affected records (lists/updates).
    /// </summary>
    public int? AffectedCount { get; private set; }

    /// <summary>
    /// Optional category-specific payload in graduated detail depth. Never contains token
    /// material or binary content (hashes/metadata instead).
    /// </summary>
    public string? PayloadJson { get; private set; }

    /// <summary>
    /// Optional link to the related approval/action request.
    /// </summary>
    public Guid? RelatedRequestId { get; private set; }

    /// <summary>
    /// Hash of the canonicalized predecessor entry of the same chain, or
    /// <see langword="null"/> for the first entry.
    /// </summary>
    public string? PrevHash { get; private set; }

    /// <summary>
    /// Hash over the canonicalized content of this entry (chain anchor).
    /// </summary>
    public string EntryHash { get; private set; } = null!;

    /// <summary>
    /// Field deltas (before/after) of the records changed by this entry.
    /// </summary>
    public ICollection<McpAuditEntryChange> Changes { get; } = new List<McpAuditEntryChange>();

    /// <summary>
    /// Attaches a field delta to this entry.
    /// </summary>
    /// <param name="change">The change record to attach; it must reference this entry.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="change"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the change references a different entry.</exception>
    public void AddChange(McpAuditEntryChange change)
    {
        if (change == null)
        {
            throw new ArgumentNullException(nameof(change));
        }
        if (change.EntryId != Id)
        {
            throw new ArgumentException("The change must reference this audit entry.", nameof(change));
        }
        Changes.Add(change);
    }

    /// <summary>
    /// Backup DTO of <see cref="McpAuditEntry"/> including the chain fields, so the hash chain
    /// stays verifiable after a restore.
    /// </summary>
    /// <param name="Id">Entity identifier.</param>
    /// <param name="Utc">Server-side timestamp of the event.</param>
    /// <param name="SequenceNo">Monotonic sequence number of the chain.</param>
    /// <param name="Category">Event category.</param>
    /// <param name="EventType">Stable event type code.</param>
    /// <param name="OwnerUserId">Affected owner, or <see langword="null"/> for global events.</param>
    /// <param name="McpAccessId">Optional soft reference to the causing access.</param>
    /// <param name="McpAccessName">Optional snapshot of the access name.</param>
    /// <param name="ToolName">Optional tool name.</param>
    /// <param name="CatalogVersion">Optional tool catalog version.</param>
    /// <param name="Context">Optional administrative context.</param>
    /// <param name="ResultStatus">Stable result code.</param>
    /// <param name="CorrelationId">Optional correlation id.</param>
    /// <param name="SourceIp">Optional source IP.</param>
    /// <param name="DurationMs">Optional duration in milliseconds.</param>
    /// <param name="AffectedCount">Optional number of affected records.</param>
    /// <param name="PayloadJson">Optional category-specific payload.</param>
    /// <param name="RelatedRequestId">Optional link to the related request.</param>
    /// <param name="PrevHash">Hash of the predecessor entry of the same chain.</param>
    /// <param name="EntryHash">Hash over the entry content.</param>
    /// <param name="CreatedUtc">Creation timestamp.</param>
    /// <returns>The result.</returns>
    public sealed record McpAuditEntryBackupDto(
        Guid Id,
        DateTime Utc,
        long SequenceNo,
        McpAuditCategory Category,
        string EventType,
        Guid? OwnerUserId,
        Guid? McpAccessId,
        string? McpAccessName,
        string? ToolName,
        string? CatalogVersion,
        string? Context,
        string ResultStatus,
        Guid? CorrelationId,
        string? SourceIp,
        int? DurationMs,
        int? AffectedCount,
        string? PayloadJson,
        Guid? RelatedRequestId,
        string? PrevHash,
        string EntryHash,
        DateTime CreatedUtc);

    /// <summary>
    /// Creates a backup DTO representing the serializable state of this audit entry
    /// (including the hash chain fields).
    /// </summary>
    /// <returns>A <see cref="McpAuditEntryBackupDto"/> containing the backup data.</returns>
    public McpAuditEntryBackupDto ToBackupDto()
        => new McpAuditEntryBackupDto(
            Id,
            Utc,
            SequenceNo,
            Category,
            EventType,
            OwnerUserId,
            McpAccessId,
            McpAccessName,
            ToolName,
            CatalogVersion,
            Context,
            ResultStatus,
            CorrelationId,
            SourceIp,
            DurationMs,
            AffectedCount,
            PayloadJson,
            RelatedRequestId,
            PrevHash,
            EntryHash,
            CreatedUtc);

    /// <summary>
    /// Applies values from a backup DTO to this entry. Audit entries are append-only and carry
    /// their chain fields verbatim so the restored chain remains verifiable.
    /// </summary>
    /// <param name="dto">The backup DTO to apply to this entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is <c>null</c>.</exception>
    public void AssignBackupDto(McpAuditEntryBackupDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        Utc = dto.Utc;
        SequenceNo = dto.SequenceNo;
        Category = dto.Category;
        EventType = dto.EventType;
        OwnerUserId = dto.OwnerUserId;
        McpAccessId = dto.McpAccessId;
        McpAccessName = dto.McpAccessName;
        ToolName = dto.ToolName;
        CatalogVersion = dto.CatalogVersion;
        Context = dto.Context;
        ResultStatus = dto.ResultStatus;
        CorrelationId = dto.CorrelationId;
        SourceIp = dto.SourceIp;
        DurationMs = dto.DurationMs;
        AffectedCount = dto.AffectedCount;
        PayloadJson = dto.PayloadJson;
        RelatedRequestId = dto.RelatedRequestId;
        PrevHash = dto.PrevHash;
        EntryHash = dto.EntryHash;
        SetDates(dto.CreatedUtc, null);
    }
}

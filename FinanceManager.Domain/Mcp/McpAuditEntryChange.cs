namespace FinanceManager.Domain.Mcp;

/// <summary>
/// A field delta (before/after) of one record changed by an <see cref="McpAuditEntry"/>.
/// Kept as a separate child table so drilling queries ("which MCP action touched this record")
/// run relationally instead of parsing JSON payloads.
/// </summary>
public sealed class McpAuditEntryChange : Entity
{
    /// <summary>
    /// Parameterless constructor for persistence/ORM and deserialization.
    /// </summary>
    private McpAuditEntryChange() { }

    /// <summary>
    /// Creates a new field delta of an audit entry.
    /// </summary>
    /// <param name="entryId">Identifier of the parent audit entry. Must not be empty.</param>
    /// <param name="entityType">Affected entity type name (e.g. <c>Posting</c>, <c>Contact</c>). Required.</param>
    /// <param name="entityId">Identifier of the affected record. Must not be empty.</param>
    /// <param name="field">Name of the changed field. Required.</param>
    /// <param name="oldValue">Serialized previous value, or <see langword="null"/>.</param>
    /// <param name="newValue">Serialized new value, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">Thrown when a required argument is empty or whitespace.</exception>
    public McpAuditEntryChange(Guid entryId,
        string entityType,
        Guid entityId,
        string field,
        string? oldValue,
        string? newValue)
    {
        EntryId = Guards.NotEmpty(entryId, nameof(entryId));
        EntityType = Guards.NotNullOrWhiteSpace(entityType, nameof(entityType));
        EntityId = Guards.NotEmpty(entityId, nameof(entityId));
        Field = Guards.NotNullOrWhiteSpace(field, nameof(field));
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>
    /// Identifier of the parent audit entry (FK, cascade delete with the entry).
    /// </summary>
    public Guid EntryId { get; private set; }

    /// <summary>
    /// Navigation to the parent audit entry.
    /// </summary>
    public McpAuditEntry? Entry { get; private set; }

    /// <summary>
    /// Affected entity type name (e.g. <c>Posting</c>, <c>Contact</c>, <c>StatementDraftEntry</c>).
    /// </summary>
    public string EntityType { get; private set; } = null!;

    /// <summary>
    /// Identifier of the affected record (drilling target).
    /// </summary>
    public Guid EntityId { get; private set; }

    /// <summary>
    /// Name of the changed field.
    /// </summary>
    public string Field { get; private set; } = null!;

    /// <summary>
    /// Serialized previous value (length-limited with truncation note), or <see langword="null"/>.
    /// </summary>
    public string? OldValue { get; private set; }

    /// <summary>
    /// Serialized new value (length-limited with truncation note), or <see langword="null"/>.
    /// </summary>
    public string? NewValue { get; private set; }

    /// <summary>
    /// Backup DTO of <see cref="McpAuditEntryChange"/>.
    /// </summary>
    /// <param name="Id">Entity identifier.</param>
    /// <param name="EntryId">Identifier of the parent audit entry.</param>
    /// <param name="EntityType">Affected entity type name.</param>
    /// <param name="EntityId">Identifier of the affected record.</param>
    /// <param name="Field">Name of the changed field.</param>
    /// <param name="OldValue">Serialized previous value.</param>
    /// <param name="NewValue">Serialized new value.</param>
    /// <param name="CreatedUtc">Creation timestamp.</param>
    /// <returns>The result.</returns>
    public sealed record McpAuditEntryChangeBackupDto(
        Guid Id,
        Guid EntryId,
        string EntityType,
        Guid EntityId,
        string Field,
        string? OldValue,
        string? NewValue,
        DateTime CreatedUtc);

    /// <summary>
    /// Creates a backup DTO representing the serializable state of this change record.
    /// </summary>
    /// <returns>A <see cref="McpAuditEntryChangeBackupDto"/> containing the backup data.</returns>
    public McpAuditEntryChangeBackupDto ToBackupDto()
        => new McpAuditEntryChangeBackupDto(
            Id,
            EntryId,
            EntityType,
            EntityId,
            Field,
            OldValue,
            NewValue,
            CreatedUtc);

    /// <summary>
    /// Applies values from a backup DTO to this change record.
    /// </summary>
    /// <param name="dto">The backup DTO to apply to this entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is <c>null</c>.</exception>
    public void AssignBackupDto(McpAuditEntryChangeBackupDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        EntityType = dto.EntityType;
        EntityId = dto.EntityId;
        Field = dto.Field;
        OldValue = dto.OldValue;
        NewValue = dto.NewValue;
        SetDates(dto.CreatedUtc, null);
    }
}

namespace FinanceManager.Domain.Mcp;

/// <summary>
/// One approval grant (Vorab-Freigabe-Tupel) of an <see cref="McpAccess"/> in the
/// <see cref="McpApprovalMode.Granular"/> mode: the persisted (Datentyp × Rechteart) tuple
/// the application-layer action catalog consults to decide whether an operation runs
/// directly (routine operations) or its <see cref="AiActionRequest"/> starts directly
/// approved (approval-required operations).
/// </summary>
/// <remarks>
/// Grantable right kinds are <see cref="McpRightKind.Read"/>,
/// <see cref="McpRightKind.Complete"/>, <see cref="McpRightKind.Modify"/> and
/// <see cref="McpRightKind.Create"/> only — <see cref="McpRightKind.Suggest"/>,
/// <see cref="McpRightKind.Book"/> and <see cref="McpRightKind.Delete"/> can never be
/// pre-approved by a grant (final ledger/destructive effects stay approval-bound). The
/// tuple must also be a valid F3-matrix combination, and
/// <see cref="McpDataType.Administration"/> grants are only accepted for owners holding the
/// administrative role (mirroring the administration-scope rule). All invariants are
/// enforced by the access administration service before persisting.
/// </remarks>
public sealed class McpAccessApprovalGrant : Entity
{
    /// <summary>
    /// Parameterless constructor for persistence/ORM and deserialization.
    /// </summary>
    private McpAccessApprovalGrant() { }

    /// <summary>
    /// Creates a new approval grant for an access.
    /// </summary>
    /// <param name="mcpAccessId">Identifier of the owning access. Must not be empty.</param>
    /// <param name="ownerUserId">Identifier of the user owning the access. Must not be empty.</param>
    /// <param name="dataType">Data type the grant applies to.</param>
    /// <param name="right">Right kind pre-approved on the data type.</param>
    /// <exception cref="ArgumentException">Thrown when a required id is empty.</exception>
    public McpAccessApprovalGrant(Guid mcpAccessId, Guid ownerUserId, McpDataType dataType, McpRightKind right)
    {
        McpAccessId = Guards.NotEmpty(mcpAccessId, nameof(mcpAccessId));
        OwnerUserId = Guards.NotEmpty(ownerUserId, nameof(ownerUserId));
        DataType = dataType;
        Right = right;
    }

    /// <summary>
    /// Identifier of the owning MCP access (FK, cascade delete with the access).
    /// </summary>
    public Guid McpAccessId { get; private set; }

    /// <summary>
    /// Navigation to the owning access.
    /// </summary>
    public McpAccess? Access { get; private set; }

    /// <summary>
    /// Owner user identifier (denormalized for owner-scoped queries and cleanup).
    /// </summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Data type the grant applies to.
    /// </summary>
    public McpDataType DataType { get; private set; }

    /// <summary>
    /// Right kind pre-approved on the data type.
    /// </summary>
    public McpRightKind Right { get; private set; }

    /// <summary>
    /// Determines whether the right kind is grantable at all — only
    /// <see cref="McpRightKind.Read"/>, <see cref="McpRightKind.Complete"/>,
    /// <see cref="McpRightKind.Modify"/> and <see cref="McpRightKind.Create"/> may be
    /// pre-approved by a grant. <see cref="McpRightKind.Suggest"/> is meaningless for a
    /// write-decision grant, <see cref="McpRightKind.Book"/> and
    /// <see cref="McpRightKind.Delete"/> cover the final ledger/destructive effects that
    /// stay approval-bound even in the expert mode.
    /// </summary>
    /// <param name="right">The right kind to evaluate.</param>
    /// <returns><see langword="true"/> when a grant row may carry the right.</returns>
    public static bool IsGrantableRight(McpRightKind right)
        => right is McpRightKind.Read or McpRightKind.Complete or McpRightKind.Modify
            or McpRightKind.Create;

    /// <summary>
    /// Backup DTO of <see cref="McpAccessApprovalGrant"/>.
    /// </summary>
    /// <param name="Id">Entity identifier.</param>
    /// <param name="McpAccessId">Identifier of the owning access.</param>
    /// <param name="OwnerUserId">Owner user identifier.</param>
    /// <param name="DataType">Data type the grant applies to.</param>
    /// <param name="Right">Right kind pre-approved on the data type.</param>
    /// <param name="CreatedUtc">Creation timestamp.</param>
    /// <param name="ModifiedUtc">Last modification timestamp.</param>
    /// <returns>The backup record of a grant.</returns>
    public sealed record McpAccessApprovalGrantBackupDto(
        Guid Id,
        Guid McpAccessId,
        Guid OwnerUserId,
        McpDataType DataType,
        McpRightKind Right,
        DateTime CreatedUtc,
        DateTime? ModifiedUtc);

    /// <summary>
    /// Creates a backup DTO representing the serializable state of this grant.
    /// </summary>
    /// <returns>A <see cref="McpAccessApprovalGrantBackupDto"/> containing the backup data.</returns>
    public McpAccessApprovalGrantBackupDto ToBackupDto()
        => new(Id, McpAccessId, OwnerUserId, DataType, Right, CreatedUtc, ModifiedUtc);

    /// <summary>
    /// Applies values from a backup DTO to this grant. Ownership references
    /// (<see cref="McpAccessId"/>, <see cref="OwnerUserId"/>) are intentionally not
    /// reassigned; the restoring service binds the grant to the (possibly re-created)
    /// access.
    /// </summary>
    /// <param name="dto">The backup DTO to apply to this entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is <c>null</c>.</exception>
    public void AssignBackupDto(McpAccessApprovalGrantBackupDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        DataType = dto.DataType;
        Right = dto.Right;
        SetDates(dto.CreatedUtc, dto.ModifiedUtc);
    }
}

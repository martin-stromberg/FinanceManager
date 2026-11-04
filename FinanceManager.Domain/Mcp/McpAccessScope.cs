namespace FinanceManager.Domain.Mcp;

/// <summary>
/// One scope tuple (Datentyp × Rechteart × Filter) of the permission profile of an
/// <see cref="McpAccess"/>. Rows only exist for accesses with
/// <see cref="McpAccessLevel.Individuell"/>; other levels derive their effective profile from the
/// server-side level matrix at runtime.
/// </summary>
/// <remarks>
/// Filter semantics: an empty/<see langword="null"/> CSV means unrestricted, a set CSV is a
/// positive whitelist (unclassified records are not matched). Within one row the filter
/// dimensions are combined with AND; multiple rows on the same (DataType, Right) combine with OR
/// (berechtigungsmodell.md §4.2).
/// </remarks>
public sealed class McpAccessScope : Entity
{
    /// <summary>
    /// Parameterless constructor for persistence/ORM and deserialization.
    /// </summary>
    private McpAccessScope() { }

    /// <summary>
    /// Creates a new scope tuple for an access.
    /// </summary>
    /// <param name="mcpAccessId">Identifier of the owning access. Must not be empty.</param>
    /// <param name="ownerUserId">Identifier of the user owning the access. Must not be empty.</param>
    /// <param name="dataType">Data type this scope applies to.</param>
    /// <param name="right">Right kind granted on the data type.</param>
    /// <param name="accountTypesCsv">Optional CSV of <c>AccountType</c> values restricting the scope.</param>
    /// <param name="contactCategoryIdsCsv">Optional CSV of contact category ids restricting the scope.</param>
    /// <param name="securityCategoryIdsCsv">Optional CSV of security category ids restricting the scope.</param>
    /// <param name="savingsPlanCategoryIdsCsv">Optional CSV of savings plan category ids restricting the scope.</param>
    /// <param name="attachmentCategoryIdsCsv">Optional CSV of attachment category ids restricting the scope.</param>
    /// <exception cref="ArgumentException">Thrown when a required id is empty.</exception>
    public McpAccessScope(Guid mcpAccessId,
        Guid ownerUserId,
        McpDataType dataType,
        McpRightKind right,
        string? accountTypesCsv = null,
        string? contactCategoryIdsCsv = null,
        string? securityCategoryIdsCsv = null,
        string? savingsPlanCategoryIdsCsv = null,
        string? attachmentCategoryIdsCsv = null)
    {
        McpAccessId = Guards.NotEmpty(mcpAccessId, nameof(mcpAccessId));
        OwnerUserId = Guards.NotEmpty(ownerUserId, nameof(ownerUserId));
        DataType = dataType;
        Right = right;
        AccountTypesCsv = accountTypesCsv;
        ContactCategoryIdsCsv = contactCategoryIdsCsv;
        SecurityCategoryIdsCsv = securityCategoryIdsCsv;
        SavingsPlanCategoryIdsCsv = savingsPlanCategoryIdsCsv;
        AttachmentCategoryIdsCsv = attachmentCategoryIdsCsv;
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
    /// Data type this scope applies to.
    /// </summary>
    public McpDataType DataType { get; private set; }

    /// <summary>
    /// Right kind granted on the data type.
    /// </summary>
    public McpRightKind Right { get; private set; }

    /// <summary>
    /// Optional CSV of <c>AccountType</c> values restricting the scope (<see langword="null"/> = no filter).
    /// </summary>
    public string? AccountTypesCsv { get; private set; }

    /// <summary>
    /// Optional CSV of contact category ids restricting the scope (<see langword="null"/> = no filter).
    /// </summary>
    public string? ContactCategoryIdsCsv { get; private set; }

    /// <summary>
    /// Optional CSV of security category ids restricting the scope (<see langword="null"/> = no filter).
    /// </summary>
    public string? SecurityCategoryIdsCsv { get; private set; }

    /// <summary>
    /// Optional CSV of savings plan category ids restricting the scope (<see langword="null"/> = no filter).
    /// </summary>
    public string? SavingsPlanCategoryIdsCsv { get; private set; }

    /// <summary>
    /// Optional CSV of attachment category ids restricting the scope (<see langword="null"/> = no filter).
    /// </summary>
    public string? AttachmentCategoryIdsCsv { get; private set; }

    /// <summary>
    /// Replaces the filter dimensions of this scope. <see langword="null"/> removes a filter.
    /// </summary>
    /// <param name="accountTypesCsv">Optional CSV of account type values.</param>
    /// <param name="contactCategoryIdsCsv">Optional CSV of contact category ids.</param>
    /// <param name="securityCategoryIdsCsv">Optional CSV of security category ids.</param>
    /// <param name="savingsPlanCategoryIdsCsv">Optional CSV of savings plan category ids.</param>
    /// <param name="attachmentCategoryIdsCsv">Optional CSV of attachment category ids.</param>
    public void SetFilters(string? accountTypesCsv,
        string? contactCategoryIdsCsv,
        string? securityCategoryIdsCsv,
        string? savingsPlanCategoryIdsCsv,
        string? attachmentCategoryIdsCsv)
    {
        AccountTypesCsv = accountTypesCsv;
        ContactCategoryIdsCsv = contactCategoryIdsCsv;
        SecurityCategoryIdsCsv = securityCategoryIdsCsv;
        SavingsPlanCategoryIdsCsv = savingsPlanCategoryIdsCsv;
        AttachmentCategoryIdsCsv = attachmentCategoryIdsCsv;
        Touch();
    }

    /// <summary>
    /// Backup DTO of <see cref="McpAccessScope"/>. Filter CSVs are exported unchanged
    /// (category ids are stable per user).
    /// </summary>
    /// <param name="Id">Entity identifier.</param>
    /// <param name="McpAccessId">Identifier of the owning access.</param>
    /// <param name="OwnerUserId">Owner user identifier.</param>
    /// <param name="DataType">Data type this scope applies to.</param>
    /// <param name="Right">Right kind granted on the data type.</param>
    /// <param name="AccountTypesCsv">Optional CSV of account type values.</param>
    /// <param name="ContactCategoryIdsCsv">Optional CSV of contact category ids.</param>
    /// <param name="SecurityCategoryIdsCsv">Optional CSV of security category ids.</param>
    /// <param name="SavingsPlanCategoryIdsCsv">Optional CSV of savings plan category ids.</param>
    /// <param name="AttachmentCategoryIdsCsv">Optional CSV of attachment category ids.</param>
    /// <param name="CreatedUtc">Creation timestamp.</param>
    /// <param name="ModifiedUtc">Last modification timestamp.</param>
    /// <returns>The result.</returns>
    public sealed record McpAccessScopeBackupDto(
        Guid Id,
        Guid McpAccessId,
        Guid OwnerUserId,
        McpDataType DataType,
        McpRightKind Right,
        string? AccountTypesCsv,
        string? ContactCategoryIdsCsv,
        string? SecurityCategoryIdsCsv,
        string? SavingsPlanCategoryIdsCsv,
        string? AttachmentCategoryIdsCsv,
        DateTime CreatedUtc,
        DateTime? ModifiedUtc);

    /// <summary>
    /// Creates a backup DTO representing the serializable state of this scope.
    /// </summary>
    /// <returns>A <see cref="McpAccessScopeBackupDto"/> containing the backup data.</returns>
    public McpAccessScopeBackupDto ToBackupDto()
        => new McpAccessScopeBackupDto(
            Id,
            McpAccessId,
            OwnerUserId,
            DataType,
            Right,
            AccountTypesCsv,
            ContactCategoryIdsCsv,
            SecurityCategoryIdsCsv,
            SavingsPlanCategoryIdsCsv,
            AttachmentCategoryIdsCsv,
            CreatedUtc,
            ModifiedUtc);

    /// <summary>
    /// Applies values from a backup DTO to this scope. Ownership references
    /// (<see cref="McpAccessId"/>, <see cref="OwnerUserId"/>) are intentionally not reassigned;
    /// the restoring service binds the scope to the (possibly re-created) access.
    /// </summary>
    /// <param name="dto">The backup DTO to apply to this entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is <c>null</c>.</exception>
    public void AssignBackupDto(McpAccessScopeBackupDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        DataType = dto.DataType;
        Right = dto.Right;
        AccountTypesCsv = dto.AccountTypesCsv;
        ContactCategoryIdsCsv = dto.ContactCategoryIdsCsv;
        SecurityCategoryIdsCsv = dto.SecurityCategoryIdsCsv;
        SavingsPlanCategoryIdsCsv = dto.SavingsPlanCategoryIdsCsv;
        AttachmentCategoryIdsCsv = dto.AttachmentCategoryIdsCsv;
        SetDates(dto.CreatedUtc, dto.ModifiedUtc);
    }
}

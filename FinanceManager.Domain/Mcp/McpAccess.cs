namespace FinanceManager.Domain.Mcp;

/// <summary>
/// An MCP access (MCP-Zugang): a named, token-authenticated channel through which an MCP client
/// acts on behalf of the owning user. Carries the token hash, the access level (Freigabestufe),
/// the approval mode, optional security restrictions (expiry, IP binding) and the lifecycle state.
/// </summary>
/// <remarks>
/// The token is only ever stored as a one-way hash (<c>pbkdf2|&lt;iters&gt;|&lt;salt-b64&gt;|&lt;key-b64&gt;</c>);
/// the plaintext is never persisted. <see cref="AccessKeyHint"/> is the non-secret lookup key of the
/// authentication path and is unique globally. Expiration is computed from <see cref="ExpiresUtc"/>;
/// it is intentionally not part of <see cref="Status"/>.
/// </remarks>
public sealed class McpAccess : Entity, IAggregateRoot
{
    /// <summary>
    /// Parameterless constructor for persistence/ORM and deserialization.
    /// </summary>
    private McpAccess() { }

    /// <summary>
    /// Creates a new MCP access. The access starts <see cref="McpAccessStatus.Active"/> with the
    /// supplied token credential already hashed by the caller.
    /// </summary>
    /// <param name="ownerUserId">Identifier of the user owning this access. Must not be empty.</param>
    /// <param name="name">Display name of the access. Must not be null or whitespace.</param>
    /// <param name="accessKeyHint">Non-secret access key hint extracted from the token; unique lookup key.</param>
    /// <param name="tokenHash">One-way hash of the token secret (<c>pbkdf2|…</c> format). Never plaintext.</param>
    /// <param name="level">Access level (Freigabestufe).</param>
    /// <param name="approvalMode">Approval mode for approval-requiring actions.</param>
    /// <param name="description">Optional free-text note (purpose/client).</param>
    /// <param name="expiresUtc">Optional expiry timestamp; an expired access behaves like a deactivated one.</param>
    /// <param name="allowedIpRanges">Optional comma-separated CIDR/address list restricting the client IPs.</param>
    /// <exception cref="ArgumentException">Thrown when a required argument is empty or whitespace.</exception>
    public McpAccess(Guid ownerUserId,
        string name,
        string accessKeyHint,
        string tokenHash,
        McpAccessLevel level,
        McpApprovalMode approvalMode,
        string? description = null,
        DateTime? expiresUtc = null,
        string? allowedIpRanges = null)
    {
        OwnerUserId = Guards.NotEmpty(ownerUserId, nameof(ownerUserId));
        Rename(name);
        AccessKeyHint = Guards.NotNullOrWhiteSpace(accessKeyHint, nameof(accessKeyHint));
        TokenHash = Guards.NotNullOrWhiteSpace(tokenHash, nameof(tokenHash));
        Level = level;
        ApprovalMode = approvalMode;
        Description = description;
        ExpiresUtc = expiresUtc;
        AllowedIpRanges = allowedIpRanges;
        Status = McpAccessStatus.Active;
        TokenIssuedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Owner user identifier; all visibility and ownership checks depend on it.
    /// </summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Display name of the access; unique per owner.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// Optional free-text note of the user (purpose/client).
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Non-secret access key hint derived from the token (<c>fm_mcp_&lt;hint&gt;_&lt;secret&gt;</c>);
    /// global unique lookup key of the authentication path.
    /// </summary>
    public string AccessKeyHint { get; private set; } = null!;

    /// <summary>
    /// One-way hash of the token secret in <c>pbkdf2|&lt;iters&gt;|&lt;salt-b64&gt;|&lt;key-b64&gt;</c> format.
    /// Never plaintext and never reversible.
    /// </summary>
    public string TokenHash { get; private set; } = null!;

    /// <summary>
    /// Access level (Freigabestufe) determining the effective scope profile.
    /// </summary>
    public McpAccessLevel Level { get; private set; }

    /// <summary>
    /// Approval mode for approval-requiring actions.
    /// </summary>
    public McpApprovalMode ApprovalMode { get; private set; }

    /// <summary>
    /// Immediately effective lifecycle status. Expiry is computed from <see cref="ExpiresUtc"/>.
    /// </summary>
    public McpAccessStatus Status { get; private set; } = McpAccessStatus.Active;

    /// <summary>
    /// Optional expiry timestamp; after expiry the access behaves like a deactivated one.
    /// </summary>
    public DateTime? ExpiresUtc { get; private set; }

    /// <summary>
    /// Optional IP binding: comma-separated CIDR/address list of allowed client IPs.
    /// </summary>
    public string? AllowedIpRanges { get; private set; }

    /// <summary>
    /// Timestamp of the last successful authentication with this access.
    /// </summary>
    public DateTime? LastUsedUtc { get; private set; }

    /// <summary>
    /// Issue timestamp of the current token (rotation/age information).
    /// </summary>
    public DateTime TokenIssuedUtc { get; private set; }

    /// <summary>
    /// Timestamp of the revocation, or <see langword="null"/> when not revoked.
    /// </summary>
    public DateTime? RevokedUtc { get; private set; }

    /// <summary>
    /// Business reason of the revocation (e.g. user revocation or system anomaly shutdown).
    /// </summary>
    public string? RevocationReason { get; private set; }

    /// <summary>
    /// Persisted scope tuples. Only populated when <see cref="Level"/> is
    /// <see cref="McpAccessLevel.Individuell"/>; other levels derive their effective profile
    /// from the server-side level matrix at runtime.
    /// </summary>
    public ICollection<McpAccessScope> Scopes { get; } = new List<McpAccessScope>();

    /// <summary>
    /// Renames the access.
    /// </summary>
    /// <param name="name">New display name. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public void Rename(string name)
    {
        Name = Guards.NotNullOrWhiteSpace(name, nameof(name));
        Touch();
    }

    /// <summary>
    /// Updates the editable configuration of the access. When the level changes away from
    /// <see cref="McpAccessLevel.Individuell"/> all persisted scope rows are removed, because
    /// only the individual level materializes scopes.
    /// </summary>
    /// <param name="name">Display name. Must not be null or whitespace.</param>
    /// <param name="description">Optional free-text note.</param>
    /// <param name="level">Access level.</param>
    /// <param name="approvalMode">Approval mode.</param>
    /// <param name="expiresUtc">Optional expiry timestamp.</param>
    /// <param name="allowedIpRanges">Optional comma-separated allowed client IPs.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public void Update(string name,
        string? description,
        McpAccessLevel level,
        McpApprovalMode approvalMode,
        DateTime? expiresUtc,
        string? allowedIpRanges)
    {
        Rename(name);
        Description = description;
        Level = level;
        ApprovalMode = approvalMode;
        ExpiresUtc = expiresUtc;
        AllowedIpRanges = allowedIpRanges;
        if (level != McpAccessLevel.Individuell)
        {
            Scopes.Clear();
        }
        Touch();
    }

    /// <summary>
    /// Rotates the token credential: installs a new access key hint and token hash and marks the
    /// token as issued now. Rotation immediately invalidates the previous token (no grace period)
    /// and is also the reactivation path of a revoked or backup-restored access: the status
    /// becomes <see cref="McpAccessStatus.Active"/> and revocation data is cleared.
    /// </summary>
    /// <param name="newAccessKeyHint">Non-secret hint of the new token. Must not be null or whitespace.</param>
    /// <param name="newTokenHash">One-way hash of the new token secret. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when an argument is null or whitespace.</exception>
    public void RotateToken(string newAccessKeyHint, string newTokenHash)
    {
        AccessKeyHint = Guards.NotNullOrWhiteSpace(newAccessKeyHint, nameof(newAccessKeyHint));
        TokenHash = Guards.NotNullOrWhiteSpace(newTokenHash, nameof(newTokenHash));
        TokenIssuedUtc = DateTime.UtcNow;
        Status = McpAccessStatus.Active;
        RevokedUtc = null;
        RevocationReason = null;
        Touch();
    }

    /// <summary>
    /// Activates the access. A revoked access cannot be reactivated; it requires
    /// <see cref="RotateToken"/> to issue a fresh credential.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the access is revoked.</exception>
    public void Activate()
    {
        if (Status == McpAccessStatus.Revoked)
        {
            throw new InvalidOperationException("A revoked access cannot be activated; rotate the token instead.");
        }
        Status = McpAccessStatus.Active;
        Touch();
    }

    /// <summary>
    /// Deactivates the access; tokens are rejected from the next request on.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the access is revoked.</exception>
    public void Deactivate()
    {
        if (Status == McpAccessStatus.Revoked)
        {
            throw new InvalidOperationException("A revoked access cannot be deactivated.");
        }
        Status = McpAccessStatus.Deactivated;
        Touch();
    }

    /// <summary>
    /// Revokes the access (terminal state): tokens are rejected immediately and the revocation
    /// is recorded with a business reason.
    /// </summary>
    /// <param name="reason">Business reason of the revocation. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="reason"/> is null or whitespace.</exception>
    public void Revoke(string reason)
    {
        Status = McpAccessStatus.Revoked;
        RevokedUtc = DateTime.UtcNow;
        RevocationReason = Guards.NotNullOrWhiteSpace(reason, nameof(reason));
        Touch();
    }

    /// <summary>
    /// Records a successful use (authentication) of this access.
    /// </summary>
    public void RecordUse()
    {
        LastUsedUtc = DateTime.UtcNow;
        Touch();
    }

    /// <summary>
    /// Determines whether the access is currently usable: active and not expired.
    /// </summary>
    /// <param name="utcNow">The current UTC timestamp to evaluate against.</param>
    /// <returns><see langword="true"/> when the access can authenticate requests; otherwise <see langword="false"/>.</returns>
    public bool IsCurrentlyUsable(DateTime utcNow)
        => Status == McpAccessStatus.Active && !IsExpired(utcNow);

    /// <summary>
    /// Determines whether the access is expired at the given instant.
    /// </summary>
    /// <param name="utcNow">The current UTC timestamp to evaluate against.</param>
    /// <returns><see langword="true"/> when an expiry is set and lies in the past.</returns>
    public bool IsExpired(DateTime utcNow)
        => ExpiresUtc.HasValue && ExpiresUtc.Value <= utcNow;

    /// <summary>
    /// Replaces the persisted scope rows of an individually configured access.
    /// </summary>
    /// <param name="scopes">The scope tuples to persist. Each scope must reference this access.</param>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Level"/> is not <see cref="McpAccessLevel.Individuell"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a scope does not belong to this access.</exception>
    public void ReplaceIndividualScopes(IEnumerable<McpAccessScope> scopes)
    {
        if (Level != McpAccessLevel.Individuell)
        {
            throw new InvalidOperationException("Scope rows can only be persisted for an individually configured access.");
        }
        if (scopes == null)
        {
            throw new ArgumentNullException(nameof(scopes));
        }
        var list = scopes.ToList();
        foreach (var scope in list)
        {
            if (scope.McpAccessId != Id || scope.OwnerUserId != OwnerUserId)
            {
                throw new ArgumentException("All scopes must reference this access and its owner.", nameof(scopes));
            }
        }
        Scopes.Clear();
        foreach (var scope in list)
        {
            Scopes.Add(scope);
        }
        Touch();
    }

    /// <summary>
    /// Backup DTO of <see cref="McpAccess"/>. Deliberately contains no token material:
    /// neither <see cref="TokenHash"/> nor <see cref="AccessKeyHint"/> are exported, because a
    /// backup file must not contain recoverable access secrets. <see cref="Status"/> is always
    /// exported as <see cref="McpAccessStatus.Revoked"/> so a restored access can never
    /// silently reactivate a valid credential; reactivation happens via token rotation.
    /// </summary>
    /// <param name="Id">Entity identifier.</param>
    /// <param name="OwnerUserId">Owner user identifier.</param>
    /// <param name="Name">Display name.</param>
    /// <param name="Description">Optional free-text note.</param>
    /// <param name="Level">Access level.</param>
    /// <param name="ApprovalMode">Approval mode.</param>
    /// <param name="Status">Exported status; always <see cref="McpAccessStatus.Revoked"/>.</param>
    /// <param name="ExpiresUtc">Optional expiry timestamp.</param>
    /// <param name="AllowedIpRanges">Optional comma-separated allowed client IPs.</param>
    /// <param name="LastUsedUtc">Timestamp of the last successful authentication.</param>
    /// <param name="RevokedUtc">Timestamp of the revocation.</param>
    /// <param name="RevocationReason">Business reason of the revocation.</param>
    /// <param name="CreatedUtc">Creation timestamp.</param>
    /// <param name="ModifiedUtc">Last modification timestamp.</param>
    /// <returns>The result.</returns>
    public sealed record McpAccessBackupDto(
        Guid Id,
        Guid OwnerUserId,
        string Name,
        string? Description,
        McpAccessLevel Level,
        McpApprovalMode ApprovalMode,
        McpAccessStatus Status,
        DateTime? ExpiresUtc,
        string? AllowedIpRanges,
        DateTime? LastUsedUtc,
        DateTime? RevokedUtc,
        string? RevocationReason,
        DateTime CreatedUtc,
        DateTime? ModifiedUtc);

    /// <summary>
    /// Creates a backup DTO representing the serializable, non-secret configuration of this access.
    /// The status is forcibly exported as <see cref="McpAccessStatus.Revoked"/>.
    /// </summary>
    /// <returns>A <see cref="McpAccessBackupDto"/> containing the backup data.</returns>
    public McpAccessBackupDto ToBackupDto()
        => new McpAccessBackupDto(
            Id,
            OwnerUserId,
            Name,
            Description,
            Level,
            ApprovalMode,
            McpAccessStatus.Revoked,
            ExpiresUtc,
            AllowedIpRanges,
            LastUsedUtc,
            RevokedUtc,
            RevocationReason,
            CreatedUtc,
            ModifiedUtc);

    /// <summary>
    /// Applies values from a backup DTO to this access. The access is always reconstructed as
    /// revoked (a backup never restores a usable credential); the caller must supply placeholder
    /// token credentials at construction time, and reactivation happens via <see cref="RotateToken"/>.
    /// </summary>
    /// <param name="dto">The backup DTO to apply to this entity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is <c>null</c>.</exception>
    public void AssignBackupDto(McpAccessBackupDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        Rename(dto.Name);
        Description = dto.Description;
        Level = dto.Level;
        ApprovalMode = dto.ApprovalMode;
        ExpiresUtc = dto.ExpiresUtc;
        AllowedIpRanges = dto.AllowedIpRanges;
        LastUsedUtc = dto.LastUsedUtc;
        Status = McpAccessStatus.Revoked;
        RevokedUtc = dto.RevokedUtc ?? DateTime.UtcNow;
        RevocationReason = string.IsNullOrWhiteSpace(dto.RevocationReason) ? "Restored from backup" : dto.RevocationReason;
        SetDates(dto.CreatedUtc, dto.ModifiedUtc);
    }
}

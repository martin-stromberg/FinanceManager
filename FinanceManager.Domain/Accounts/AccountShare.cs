namespace FinanceManager.Domain.Accounts;

/// <summary>
/// Represents a share/permission of an account granted to another user.
/// </summary>
public sealed class AccountShare : Entity
{
    /// <summary>
    /// Creates a new account share entry.
    /// </summary>
    /// <param name="accountId">Id of the shared account.</param>
    /// <param name="userId">Id of the user the account is shared with.</param>
    /// <param name="role">Role granted to the user.</param>
    public AccountShare(Guid accountId, Guid userId, AccountShareRole role)
    {
        AccountId = Guards.NotEmpty(accountId, nameof(accountId));
        UserId = Guards.NotEmpty(userId, nameof(userId));
        Role = role;
    }

    /// <summary>
    /// Identifier of the shared account.
    /// </summary>
    public Guid AccountId { get; private set; }

    /// <summary>
    /// Identifier of the user who received the share.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Role granted by the share (e.g. read/write).
    /// </summary>
    public AccountShareRole Role { get; private set; }

    /// <summary>
    /// UTC timestamp when the share was granted.
    /// </summary>
    public DateTime GrantedUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when the share was revoked, if applicable.
    /// </summary>
    public DateTime? RevokedUtc { get; private set; }
}
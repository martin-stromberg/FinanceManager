namespace FinanceManager.Domain.Accounts;

/// <summary>
/// Represents a linked IBAN (sub-account IBAN) associated with a collection account.
/// </summary>
public sealed class AccountLinkedIban : Entity
{
    /// <summary>
    /// Creates a new linked IBAN entry for the specified collection account.
    /// </summary>
    /// <param name="accountId">Identifier of the collection account.</param>
    /// <param name="iban">The IBAN to link.</param>
    public AccountLinkedIban(Guid accountId, string iban)
    {
        AccountId = accountId;
        Iban = iban?.Trim() ?? throw new ArgumentNullException(nameof(iban));
    }

    /// <summary>
    /// Identifier of the collection account that owns this linked IBAN.
    /// </summary>
    public Guid AccountId { get; private set; }

    /// <summary>
    /// The linked IBAN value.
    /// </summary>
    public string Iban { get; private set; } = null!;

    /// <summary>
    /// Navigation property back to the owning account.
    /// </summary>
    public Account? Account { get; private set; }

    // Backup DTO
    /// <summary>
    /// Backup DTO for <see cref="AccountLinkedIban"/>.
    /// </summary>
    /// <param name="Id">Entity identifier.</param>
    /// <param name="AccountId">Identifier of the owning collection account.</param>
    /// <param name="Iban">The linked IBAN value.</param>
    /// <returns>The result.</returns>
    public sealed record AccountLinkedIbanBackupDto(Guid Id, Guid AccountId, string Iban);

    /// <summary>
    /// Creates a backup DTO representing the serializable state of this linked IBAN.
    /// </summary>
    /// <returns>An <see cref="AccountLinkedIbanBackupDto"/> containing values required for backup/restore.</returns>
    public AccountLinkedIbanBackupDto ToBackupDto()
        => new AccountLinkedIbanBackupDto(Id, AccountId, Iban);

    /// <summary>
    /// Assigns values from a backup DTO to this entity instance.
    /// </summary>
    /// <param name="dto">The <see cref="AccountLinkedIbanBackupDto"/> containing values to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the IBAN of <paramref name="dto"/> is <c>null</c>.</exception>
    public void AssignBackupDto(AccountLinkedIbanBackupDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        AccountId = dto.AccountId;
        Iban = dto.Iban?.Trim() ?? throw new ArgumentException("Backup DTO must provide an IBAN.", nameof(dto));
    }
}

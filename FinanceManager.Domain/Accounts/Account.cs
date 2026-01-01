namespace FinanceManager.Domain.Accounts;

/// <summary>
/// Aggregate root representing a user bank account with balance and metadata.
/// Encapsulates invariants and operations related to an account.
/// </summary>
public sealed class Account : Entity, IAggregateRoot
{
    /// <summary>
    /// Creates a new account for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user identifier.</param>
    /// <param name="type">Type of the account.</param>
    /// <param name="name">Display name of the account.</param>
    /// <param name="iban">Optional IBAN (may be null).</param>
    /// <param name="bankContactId">Identifier of the bank contact associated with this account.</param>
    public Account(Guid ownerUserId, AccountType type, string name, string? iban, Guid bankContactId)
    {
        OwnerUserId = Guards.NotEmpty(ownerUserId, nameof(ownerUserId));
        Type = type;
        Name = Guards.NotNullOrWhiteSpace(name, nameof(name));
        Iban = iban?.Trim();
        BankContactId = Guards.NotEmpty(bankContactId, nameof(bankContactId));
        // default expectation to Optional to preserve previous behavior
        SavingsPlanExpectation = SavingsPlanExpectation.Optional;
    }

    /// <summary>
    /// Identifier of the user who owns this account.
    /// </summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Account type (e.g. Current, Savings).
    /// </summary>
    public AccountType Type { get; private set; }

    /// <summary>
    /// Display name of the account.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// Optional IBAN of the account.
    /// </summary>
    public string? Iban { get; private set; }

    /// <summary>
    /// Current balance of the account.
    /// </summary>
    public decimal CurrentBalance { get; private set; }

    /// <summary>
    /// Bank contact identifier associated with the account.
    /// </summary>
    public Guid BankContactId { get; private set; }

    /// <summary>
    /// Optional symbol attachment id assigned to the account.
    /// </summary>
    public Guid? SymbolAttachmentId { get; private set; }

    /// <summary>
    /// Expected savings plan behavior attached to this account.
    /// </summary>
    public SavingsPlanExpectation SavingsPlanExpectation { get; private set; }

    /// <summary>
    /// Renames the account.
    /// </summary>
    /// <param name="name">New display name.</param>
    public void Rename(string name)
    {
        Name = Guards.NotNullOrWhiteSpace(name, nameof(name));
        Touch();
    }

    /// <summary>
    /// Sets or clears the IBAN for the account.
    /// </summary>
    /// <param name="iban">New IBAN value or null to clear.</param>
    public void SetIban(string? iban)
    {
        Iban = string.IsNullOrWhiteSpace(iban) ? null : iban.Trim();
        Touch();
    }

    /// <summary>
    /// Sets the bank contact for this account.
    /// </summary>
    /// <param name="bankContactId">Bank contact identifier.</param>
    public void SetBankContact(Guid bankContactId)
    {
        BankContactId = Guards.NotEmpty(bankContactId, nameof(bankContactId));
        Touch();
    }

    /// <summary>
    /// Changes the account type.
    /// </summary>
    /// <param name="type">New account type.</param>
    public void SetType(AccountType type)
    {
        if (Type != type)
        {
            Type = type;
            Touch();
        }
    }

    /// <summary>
    /// Adjusts the current balance by the specified delta.
    /// </summary>
    /// <param name="delta">Amount to add (positive) or subtract (negative) from the balance.</param>
    public void AdjustBalance(decimal delta)
    {
        CurrentBalance += delta;
        Touch();
    }

    /// <summary>
    /// Sets or clears the symbol attachment for the account.
    /// </summary>
    /// <param name="attachmentId">Attachment id to set or null to clear.</param>
    public void SetSymbolAttachment(Guid? attachmentId)
    {
        SymbolAttachmentId = attachmentId == Guid.Empty ? null : attachmentId;
        Touch();
    }

    /// <summary>
    /// Sets the savings plan expectation for this account.
    /// </summary>
    /// <param name="expectation">Savings plan expectation value.</param>
    public void SetSavingsPlanExpectation(SavingsPlanExpectation expectation)
    {
        if (SavingsPlanExpectation != expectation)
        {
            SavingsPlanExpectation = expectation;
            Touch();
        }
    }
}
namespace FinanceManager.Shared.Dtos.Accounts;

/// <summary>
/// DTO representing a bank account and its core properties used by the client UI.
/// </summary>
public sealed record AccountDto(
    Guid Id,
    string Name,
    AccountType Type,
    string? Iban,
    decimal CurrentBalance,
    Guid BankContactId,
    Guid? SymbolAttachmentId,
    SavingsPlanExpectation SavingsPlanExpectation,
    bool SecurityProcessingEnabled)
{
    /// <summary>
    /// Indicates whether this account is a collection account grouping multiple sub-IBANs.
    /// </summary>
    public bool IsCollectionAccount { get; init; } = false;

    /// <summary>
    /// List of linked sub-IBANs; empty for non-collection accounts.
    /// </summary>
    public IReadOnlyList<string> LinkedIbans { get; init; } = Array.Empty<string>();
}

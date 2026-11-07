namespace FinanceManager.Shared.Dtos.Accounts;

/// <summary>
/// Defines the type of a bank account.
/// </summary>
public enum AccountType
{
    /// <summary>Checking/current account.</summary>
    Giro = 0,
    /// <summary>Savings account.</summary>
    Savings = 1,
    /// <summary>Credit card settlement account. Statement drafts of this account type
    /// may only be booked indirectly as a split of a parent statement entry (the
    /// debiting giro statement line) — never standalone.</summary>
    CreditCard = 2
}

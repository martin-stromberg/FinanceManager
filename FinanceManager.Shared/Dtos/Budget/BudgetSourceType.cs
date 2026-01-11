namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Identifies the type of source used to resolve actual values for a budget purpose.
/// </summary>
public enum BudgetSourceType
{
    /// <summary>
    /// Actuals are derived from postings assigned to a single contact.
    /// </summary>
    Contact = 0,

    /// <summary>
    /// Actuals are derived from postings assigned to contacts of a contact group/category.
    /// </summary>
    ContactGroup = 1,

    /// <summary>
    /// Actuals are derived from savings plan postings of a savings plan.
    /// </summary>
    SavingsPlan = 2
}

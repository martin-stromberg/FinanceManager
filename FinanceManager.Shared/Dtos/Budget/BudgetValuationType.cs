namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Determines how matching postings are valued for a budget purpose.
/// </summary>
public enum BudgetValuationType
{
    /// <summary>
    /// Match postings by the expected budget sign. This is the backwards-compatible default.
    /// </summary>
    ExactPostings = 0,

    /// <summary>
    /// Value all matching postings together, regardless of sign.
    /// </summary>
    TotalBudget = 1
}

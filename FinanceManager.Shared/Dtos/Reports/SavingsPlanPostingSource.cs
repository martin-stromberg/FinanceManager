namespace FinanceManager.Shared.Dtos.Reports;

/// <summary>
/// Selects which postings a report uses as the data source for emitted savings plans:
/// the savings plan's own postings or the bank postings on the accounts linked to the plan.
/// Only meaningful for savings plans that have account links; plans without links always
/// report their own plan postings.
/// </summary>
public enum SavingsPlanPostingSource
{
    /// <summary>The savings plan's own postings (<see cref="FinanceManager.Shared.Dtos.Postings.PostingKind.SavingsPlan"/>).</summary>
    PlanPostings = 0,

    /// <summary>The bank postings (<see cref="FinanceManager.Shared.Dtos.Postings.PostingKind.Bank"/>) on the accounts linked to the savings plan.</summary>
    LinkedAccountPostings = 1
}

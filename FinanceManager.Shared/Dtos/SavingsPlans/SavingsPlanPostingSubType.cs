namespace FinanceManager.Shared.Dtos.SavingsPlans;

/// <summary>
/// Sub-types used for detailed savings plan posting categorization. The value is optional on a
/// posting - postings without a sub type are regular contribution or disbursement postings.
/// </summary>
public enum SavingsPlanPostingSubType
{
    /// <summary>
    /// Actual-balance report ("Istmeldung"): a manual correction posting that adjusts the plan's
    /// accumulated amount to a reported real balance. Only allowed for savings plans without
    /// linked accounts. It produces no account movement and is excluded from budget actuals.
    /// </summary>
    ActualReport = 0
}

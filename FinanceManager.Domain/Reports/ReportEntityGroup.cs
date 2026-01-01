namespace FinanceManager.Domain.Reports;

/// <summary>
/// Primary grouping axis for configurable reports.
/// </summary>
public enum ReportEntityGroup
{
    /// <summary>
    /// Group results by account.
    /// </summary>
    Account = 0,

    /// <summary>
    /// Group results by contact.
    /// </summary>
    Contact = 1,

    /// <summary>
    /// Group results by savings plan.
    /// </summary>
    SavingsPlan = 2,

    /// <summary>
    /// Group results by security.
    /// </summary>
    Security = 3
}

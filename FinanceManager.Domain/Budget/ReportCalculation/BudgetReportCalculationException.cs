namespace FinanceManager.Domain.Budget.ReportCalculation;

/// <summary>
/// Thrown when the budget report calculation encounters an invalid state, such as an invalid
/// report period or an invalid <see cref="BudgetRule"/> configuration.
/// </summary>
public sealed class BudgetReportCalculationException : Exception
{
    /// <summary>
    /// Creates a new <see cref="BudgetReportCalculationException"/> with the given message.
    /// </summary>
    /// <param name="message">Description of the invalid state.</param>
    public BudgetReportCalculationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new <see cref="BudgetReportCalculationException"/> with the given message and inner exception.
    /// </summary>
    /// <param name="message">Description of the invalid state.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public BudgetReportCalculationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Application.Budget;

/// <summary>
/// Planned value calculation result.
/// </summary>
public sealed record BudgetPlannedValuesResult(
    BudgetPeriodKey From,
    BudgetPeriodKey To,
    IReadOnlyList<BudgetPlannedValue> Values)
{
    /// <summary>
    /// Returns planned value for a specific purpose and period.
    /// </summary>
    public decimal GetPlanned(Guid purposeId, BudgetPeriodKey period)
    {
        var match = Values.FirstOrDefault(v => v.BudgetPurposeId == purposeId && v.Period == period);
        return match?.Amount ?? 0m;
    }
}

/// <summary>
/// A planned amount for a budget purpose and period.
/// </summary>
public sealed record BudgetPlannedValue(Guid BudgetPurposeId, BudgetPeriodKey Period, decimal Amount);

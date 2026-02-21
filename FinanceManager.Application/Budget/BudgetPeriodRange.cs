using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Application.Budget;

/// <summary>
/// Helper for iterating monthly period keys.
/// </summary>
internal static class BudgetPeriodRange
{
    public static IEnumerable<BudgetPeriodKey> Enumerate(BudgetPeriodKey from, BudgetPeriodKey to)
    {
        from.Validate();
        to.Validate();

        var cur = new BudgetPeriodKey(from.Year, from.Month);
        var end = new BudgetPeriodKey(to.Year, to.Month);

        while (cur.Year < end.Year || (cur.Year == end.Year && cur.Month <= end.Month))
        {
            yield return cur;
            cur = cur.AddMonths(1);
        }
    }
}

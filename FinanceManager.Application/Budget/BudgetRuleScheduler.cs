using FinanceManager.Domain.Budget;
using FinanceManager.Shared.Dtos.Budget;

namespace FinanceManager.Application.Budget;

/// <summary>
/// Deterministic rule schedule generator.
/// </summary>
internal static class BudgetRuleScheduler
{
    public static IEnumerable<BudgetPeriodKey> GetDuePeriods(BudgetRule rule, BudgetPeriodKey queryFrom, BudgetPeriodKey queryTo)
    {
        ArgumentNullException.ThrowIfNull(rule);

        queryFrom.Validate();
        queryTo.Validate();

        var start = BudgetPeriodKey.FromDate(rule.StartDate);
        start.Validate();

        var end = rule.EndDate != null ? BudgetPeriodKey.FromDate(rule.EndDate.Value) : queryTo;
        end.Validate();

        var effectiveFrom = Max(start, queryFrom);
        var effectiveTo = Min(end, queryTo);

        if (IsAfter(effectiveFrom, effectiveTo))
        {
            yield break;
        }

        var step = rule.GetIntervalStepMonths();
        if (step < 1)
        {
            step = 1;
        }

        // Anchor to rule start month and move forward until reaching effectiveFrom.
        var cur = start;
        while (IsAfter(effectiveFrom, cur))
        {
            cur = cur.AddMonths(step);
        }

        while (IsAfter(cur, effectiveTo) == false)
        {
            yield return cur;
            cur = cur.AddMonths(step);
        }
    }

    private static bool IsAfter(BudgetPeriodKey a, BudgetPeriodKey b)
    {
        return a.Year > b.Year || (a.Year == b.Year && a.Month > b.Month);
    }

    private static BudgetPeriodKey Max(BudgetPeriodKey a, BudgetPeriodKey b)
    {
        return IsAfter(a, b) ? a : b;
    }

    private static BudgetPeriodKey Min(BudgetPeriodKey a, BudgetPeriodKey b)
    {
        return IsAfter(a, b) ? b : a;
    }
}

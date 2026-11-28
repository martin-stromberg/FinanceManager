using FinanceManager.Domain.Savings;
using FinanceManager.Shared.Dtos.SavingsPlans;

namespace FinanceManager.Tests.Savings;

/// <summary>
/// Covers the month-end arithmetic used to enumerate recurring due dates:
/// <see cref="SavingsPlan.AddIntervalWithMonthEndRule"/> and
/// <see cref="SavingsPlan.SubtractIntervalWithMonthEndRule"/>.
/// </summary>
public sealed class SavingsPlanIntervalMathTests
{
    /// <summary>
    /// Month-end dates stay month-end when an interval is added (Jan 31 → Feb 28/29 → Mar 31).
    /// </summary>
    [Fact]
    public void Add_MonthEnd_StaysMonthEnd()
    {
        Assert.Equal(new DateTime(2024, 2, 29), SavingsPlan.AddIntervalWithMonthEndRule(new DateTime(2024, 1, 31), SavingsPlanInterval.Monthly));
        Assert.Equal(new DateTime(2024, 3, 31), SavingsPlan.AddIntervalWithMonthEndRule(new DateTime(2024, 2, 29), SavingsPlanInterval.Monthly));
        Assert.Equal(new DateTime(2023, 2, 28), SavingsPlan.AddIntervalWithMonthEndRule(new DateTime(2023, 1, 31), SavingsPlanInterval.Monthly));
    }

    /// <summary>
    /// Non-month-end days are capped to the length of the target month (Jan 30 → Feb 29 in a leap year).
    /// </summary>
    [Fact]
    public void Add_DayOfMonth_CappedToTargetMonth()
    {
        Assert.Equal(new DateTime(2024, 2, 29), SavingsPlan.AddIntervalWithMonthEndRule(new DateTime(2024, 1, 30), SavingsPlanInterval.Monthly));
        Assert.Equal(new DateTime(2024, 4, 30, 10, 30, 0, DateTimeKind.Utc), SavingsPlan.AddIntervalWithMonthEndRule(new DateTime(2024, 1, 30, 10, 30, 0, DateTimeKind.Utc), SavingsPlanInterval.Quarterly));
    }

    /// <summary>
    /// Every interval maps to its month count (1/2/3/6/12).
    /// </summary>
    /// <param name="interval">The recurrence interval to test.</param>
    /// <param name="months">Expected number of months the interval advances.</param>
    [Theory]
    [InlineData(SavingsPlanInterval.Monthly, 1)]
    [InlineData(SavingsPlanInterval.BiMonthly, 2)]
    [InlineData(SavingsPlanInterval.Quarterly, 3)]
    [InlineData(SavingsPlanInterval.SemiAnnually, 6)]
    [InlineData(SavingsPlanInterval.Annually, 12)]
    public void Add_AllIntervals_MapToMonthCount(SavingsPlanInterval interval, int months)
    {
        var date = new DateTime(2024, 6, 15);
        var expected = date.AddMonths(months);
        Assert.Equal(expected, SavingsPlan.AddIntervalWithMonthEndRule(date, interval));
    }

    /// <summary>
    /// Subtracting mirrors addition: month-end dates stay month-end backwards (Mar 31 → Feb 29 → Jan 31).
    /// </summary>
    [Fact]
    public void Subtract_MonthEnd_StaysMonthEnd()
    {
        Assert.Equal(new DateTime(2024, 2, 29), SavingsPlan.SubtractIntervalWithMonthEndRule(new DateTime(2024, 3, 31), SavingsPlanInterval.Monthly));
        Assert.Equal(new DateTime(2024, 1, 31), SavingsPlan.SubtractIntervalWithMonthEndRule(new DateTime(2024, 2, 29), SavingsPlanInterval.Monthly));
        Assert.Equal(new DateTime(2023, 2, 28), SavingsPlan.SubtractIntervalWithMonthEndRule(new DateTime(2023, 3, 31), SavingsPlanInterval.Monthly));
    }

    /// <summary>
    /// Non-month-end days are capped to the length of the target month when subtracting (Mar 30 → Feb 29).
    /// </summary>
    [Fact]
    public void Subtract_DayOfMonth_CappedToTargetMonth()
    {
        Assert.Equal(new DateTime(2024, 2, 29), SavingsPlan.SubtractIntervalWithMonthEndRule(new DateTime(2024, 3, 30), SavingsPlanInterval.Monthly));
        Assert.Equal(new DateTime(2024, 2, 29), SavingsPlan.SubtractIntervalWithMonthEndRule(new DateTime(2024, 8, 30), SavingsPlanInterval.SemiAnnually));
    }

    /// <summary>
    /// Subtract preserves time-of-day and Kind like the add variant.
    /// </summary>
    [Fact]
    public void Subtract_PreservesTimeAndKind()
    {
        var date = new DateTime(2024, 6, 15, 10, 30, 45, DateTimeKind.Utc);
        var result = SavingsPlan.SubtractIntervalWithMonthEndRule(date, SavingsPlanInterval.Quarterly);
        Assert.Equal(new DateTime(2024, 3, 15, 10, 30, 45, DateTimeKind.Utc), result);
    }

    /// <summary>
    /// Unsupported interval values throw on both directions.
    /// </summary>
    [Fact]
    public void AddAndSubtract_UnsupportedInterval_Throws()
    {
        var date = new DateTime(2024, 1, 15);
        Assert.Throws<ArgumentOutOfRangeException>(() => SavingsPlan.AddIntervalWithMonthEndRule(date, (SavingsPlanInterval)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => SavingsPlan.SubtractIntervalWithMonthEndRule(date, (SavingsPlanInterval)99));
    }
}

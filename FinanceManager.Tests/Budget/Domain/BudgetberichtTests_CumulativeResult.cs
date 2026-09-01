using FinanceManager.Domain.Budget.ReportCalculation;
using FinanceManager.Shared.Dtos.Budget;
using FluentAssertions;
using Xunit;
using static FinanceManager.Tests.Budget.Domain.BudgetberichtTestFixtures;

namespace FinanceManager.Tests.Budget.Domain;

/// <summary>
/// Tests for <c>Budgetbericht.GetCumulativeResult()</c> (Output phase): interval bucket aggregation.
/// </summary>
public sealed class BudgetberichtTests_CumulativeResult
{
    /// <summary>
    /// Verifies that with a monthly reporting interval, each month of the report gets its own bucket
    /// labeled "MM/yyyy" and carries the monthly rule's amount unchanged - the baseline case before
    /// any cross-month aggregation is involved.
    /// </summary>
    [Fact]
    public void GetCumulativeResult_AggregatesByMonth()
    {
        var purpose = CreatePurpose("Rent", BudgetSourceType.Contact, Guid.NewGuid());
        var rule = CreatePurposeRule(purpose.Id, -500m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 3, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });
        budgetbericht.Finish();

        var buckets = budgetbericht.GetCumulativeResult();

        buckets.Should().HaveCount(3);
        buckets.Select(b => b.IntervalLabel).Should().Equal("01/2026", "02/2026", "03/2026");
        buckets.Should().OnlyContain(b => b.BudgetedAmount == -500m);
    }

    /// <summary>
    /// Verifies that with a quarterly reporting interval, a monthly rule's amount is summed across the
    /// three months belonging to each quarter into a single "QN/yyyy" bucket, rather than being reported
    /// per month or averaged.
    /// </summary>
    [Fact]
    public void GetCumulativeResult_AggregatesByQuarter()
    {
        var purpose = CreatePurpose("Rent", BudgetSourceType.Contact, Guid.NewGuid());
        var rule = CreatePurposeRule(purpose.Id, -500m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 6, BudgetReportInterval.Quarter, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });
        budgetbericht.Finish();

        var buckets = budgetbericht.GetCumulativeResult();

        buckets.Should().HaveCount(2);
        buckets.Select(b => b.IntervalLabel).Should().Equal("Q1/2026", "Q2/2026");
        buckets.Should().OnlyContain(b => b.BudgetedAmount == -1500m, "each quarter bucket sums 3 monthly occurrences of -500");
    }

    /// <summary>
    /// Verifies that with a yearly reporting interval, a report period spanning a calendar-year boundary
    /// (Nov 2025 - Feb 2026) is split into buckets keyed by actual calendar year - each getting only the
    /// months that fall within it (two months each) - rather than one bucket per report-relative year.
    /// </summary>
    [Fact]
    public void GetCumulativeResult_AggregatesByYear()
    {
        var purpose = CreatePurpose("Rent", BudgetSourceType.Contact, Guid.NewGuid());
        var rule = CreatePurposeRule(purpose.Id, -500m, BudgetIntervalType.Monthly, new DateOnly(2025, 11, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2025, 11, 1), 4, BudgetReportInterval.Year, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });
        budgetbericht.Finish();

        var buckets = budgetbericht.GetCumulativeResult();

        buckets.Should().HaveCount(2);
        var bucket2025 = buckets.Single(b => b.IntervalLabel == "2025");
        var bucket2026 = buckets.Single(b => b.IntervalLabel == "2026");
        bucket2025.BudgetedAmount.Should().Be(-1000m, "Nov + Dec 2025");
        bucket2026.BudgetedAmount.Should().Be(-1000m, "Jan + Feb 2026");
    }

    /// <summary>
    /// Verifies that a bucket's <c>Deviation</c> and <c>DeviationPercentage</c> reflect the gap between
    /// budgeted and actual amount for that specific bucket (here: 50 of 500, i.e. 10%), confirming the
    /// deviation is computed per bucket rather than only on the overall report total.
    /// </summary>
    [Fact]
    public void GetCumulativeResult_CalculatesDeviationAndPercentagePerBucket()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Rent", BudgetSourceType.Contact, contactId);
        var rule = CreatePurposeRule(purpose.Id, -500m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });
        budgetbericht.AddPosting(CreateContactPosting(-450m, new DateTime(2026, 1, 5), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var bucket = budgetbericht.GetCumulativeResult().Single();

        bucket.Deviation.Should().Be(50m);
        bucket.DeviationPercentage.Should().Be(10m);
    }

    /// <summary>
    /// Verifies that a posting which matches no purpose or category (routed as unbudgeted) still counts
    /// toward the bucket's <c>ActualAmount</c> - the cumulative view must reflect all real money movement,
    /// not only the postings that were successfully matched to a budget expectation.
    /// </summary>
    [Fact]
    public void GetCumulativeResult_IncludesUnbudgetedPostings_InActualAmount()
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());
        budgetbericht.AddPosting(CreateUnattributedPosting(-25m, new DateTime(2026, 1, 5)), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var bucket = budgetbericht.GetCumulativeResult().Single();

        bucket.ActualAmount.Should().Be(-25m);
    }
}

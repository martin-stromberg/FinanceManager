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

using FinanceManager.Domain.Budget.ReportCalculation;
using FinanceManager.Shared.Dtos.Budget;
using FluentAssertions;
using Xunit;

namespace FinanceManager.Tests.Budget.Domain;

/// <summary>
/// Tests for the <c>Budgetbericht</c> constructor (Initialization phase).
/// </summary>
public sealed class BudgetberichtTests_Initialization
{
    /// <summary>
    /// Verifies that the constructor pre-allocates exactly one <c>MonthlyResult</c> per month of the
    /// requested period, starting from the observation date's month - the internal per-month buckets
    /// that later planning and posting-assignment steps populate must exist upfront.
    /// </summary>
    [Fact]
    public void Constructor_CreatesOneMonthlyResultPerMonth_ForGivenPeriod()
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2026, 3, 15), 4, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);

        budgetbericht.MonthlyResults.Should().HaveCount(4);
        budgetbericht.MonthlyResults.Select(m => m.Month).Should().Equal(
            new DateTime(2026, 3, 1),
            new DateTime(2026, 4, 1),
            new DateTime(2026, 5, 1),
            new DateTime(2026, 6, 1));
    }

    /// <summary>
    /// Verifies that an observation date ("Betrachtungsdatum") given mid-month (the 27th) is normalized
    /// down to the 1st of that month for bucketing purposes, so the day-of-month a caller happens to pass
    /// in never shifts which month's <c>MonthlyResult</c> the report starts at.
    /// </summary>
    [Fact]
    public void Constructor_NormalizesBetrachtungsDatumToFirstOfMonth()
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2026, 3, 27), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);

        budgetbericht.MonthlyResults.Single().Month.Should().Be(new DateTime(2026, 3, 1));
    }

    /// <summary>
    /// Verifies that a freshly constructed report has empty expectation groups and posting lists for
    /// each month - before <c>SetPlanung</c>/<c>AddPosting</c> are called, nothing should be pre-populated
    /// or default to a non-empty state that later assertions could mistake for real data.
    /// </summary>
    [Fact]
    public void Constructor_CreatesEmptyExpectationGroupsAndPostingLists_ForEachMonth()
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);

        var monthResult = budgetbericht.MonthlyResults.Single();
        monthResult.ExpectationGroups.Should().BeEmpty();
        monthResult.UnbudgetedPostings.Should().BeEmpty();
        monthResult.CostNeutralPostings.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that constructing a report with a zero or negative month count ("Anzahl Monate") is
    /// rejected with <see cref="BudgetReportCalculationException"/> - a non-positive period has no
    /// meaningful set of months to build <c>MonthlyResults</c> for.
    /// </summary>
    /// <param name="anzahlMonate">The (invalid) month count to construct the report with.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_Throws_WhenAnzahlMonateIsNotPositive(int anzahlMonate)
    {
        var act = () => new Budgetbericht(new DateOnly(2026, 1, 1), anzahlMonate, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);

        act.Should().Throw<BudgetReportCalculationException>();
    }

    /// <summary>
    /// Verifies that constructing a report with a default (uninitialized) observation date is rejected
    /// with <see cref="BudgetReportCalculationException"/>, guarding against a caller accidentally
    /// forgetting to supply a real date and silently anchoring the report on <c>DateOnly.MinValue</c>.
    /// </summary>
    [Fact]
    public void Constructor_Throws_WhenBetrachtungsDatumIsDefault()
    {
        var act = () => new Budgetbericht(default, 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);

        act.Should().Throw<BudgetReportCalculationException>();
    }

    /// <summary>
    /// Verifies that <c>MonthlyResults</c> is always returned in ascending chronological order regardless
    /// of internal construction order - downstream consumers (e.g. cumulative bucketing) rely on this
    /// ordering rather than re-sorting themselves.
    /// </summary>
    [Fact]
    public void MonthlyResults_AreInChronologicalOrder()
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2025, 11, 1), 3, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);

        budgetbericht.MonthlyResults.Select(m => m.Month).Should().BeInAscendingOrder();
    }
}

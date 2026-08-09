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

    [Fact]
    public void Constructor_NormalizesBetrachtungsDatumToFirstOfMonth()
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2026, 3, 27), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);

        budgetbericht.MonthlyResults.Single().Month.Should().Be(new DateTime(2026, 3, 1));
    }

    [Fact]
    public void Constructor_CreatesEmptyExpectationGroupsAndPostingLists_ForEachMonth()
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);

        var monthResult = budgetbericht.MonthlyResults.Single();
        monthResult.ExpectationGroups.Should().BeEmpty();
        monthResult.UnbudgetedPostings.Should().BeEmpty();
        monthResult.CostNeutralPostings.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_Throws_WhenAnzahlMonateIsNotPositive(int anzahlMonate)
    {
        var act = () => new Budgetbericht(new DateOnly(2026, 1, 1), anzahlMonate, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);

        act.Should().Throw<BudgetReportCalculationException>();
    }

    [Fact]
    public void Constructor_Throws_WhenBetrachtungsDatumIsDefault()
    {
        var act = () => new Budgetbericht(default, 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);

        act.Should().Throw<BudgetReportCalculationException>();
    }

    [Fact]
    public void MonthlyResults_AreInChronologicalOrder()
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2025, 11, 1), 3, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);

        budgetbericht.MonthlyResults.Select(m => m.Month).Should().BeInAscendingOrder();
    }
}

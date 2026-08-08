using FinanceManager.Domain.Budget.ReportCalculation;
using FinanceManager.Shared.Dtos.Budget;
using FluentAssertions;
using Xunit;
using static FinanceManager.Tests.Budget.Domain.BudgetberichtTestFixtures;

namespace FinanceManager.Tests.Budget.Domain;

/// <summary>
/// Tests for <c>Budgetbericht.SetPlanung()</c> (Planning phase): rule expansion into monthly expectations
/// and the resulting category/purpose grouping.
/// </summary>
public sealed class BudgetberichtTests_Planning
{
    [Fact]
    public void SetPlanung_ExpandsMonthlyRule_IntoOneExpectationPerMonth()
    {
        var category = CreateCategory("Housing");
        var purpose = CreatePurpose("Rent", BudgetSourceType.Contact, Guid.NewGuid(), category.Id);
        var rule = CreatePurposeRule(purpose.Id, -500m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 3, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(new[] { category }, new[] { purpose }, new[] { rule });

        foreach (var monthResult in budgetbericht.MonthlyResults)
        {
            var purposeExpectation = monthResult.ExpectationGroups.Single().Purposes.Single();
            purposeExpectation.SumExpectedAmount.Should().Be(-500m);
        }
    }

    [Fact]
    public void SetPlanung_ExpandsQuarterlyRule_OnlyIntoQuarterHomeMonths()
    {
        var category = CreateCategory("Insurance");
        var purpose = CreatePurpose("Car insurance", BudgetSourceType.Contact, Guid.NewGuid(), category.Id);
        var rule = CreatePurposeRule(purpose.Id, -120m, BudgetIntervalType.Quarterly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 6, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(new[] { category }, new[] { purpose }, new[] { rule });

        var expectedAmounts = budgetbericht.MonthlyResults
            .Select(m => m.ExpectationGroups.Single().Purposes.Single().SumExpectedAmount)
            .ToList();

        expectedAmounts.Should().Equal(-120m, 0m, 0m, -120m, 0m, 0m);
    }

    [Fact]
    public void SetPlanung_ExpandsYearlyRule_OnlyIntoStartMonth()
    {
        var category = CreateCategory("Insurance");
        var purpose = CreatePurpose("Home insurance", BudgetSourceType.Contact, Guid.NewGuid(), category.Id);
        var rule = CreatePurposeRule(purpose.Id, -240m, BudgetIntervalType.Yearly, new DateOnly(2026, 3, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 12, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(new[] { category }, new[] { purpose }, new[] { rule });

        var expectedAmounts = budgetbericht.MonthlyResults
            .Select(m => m.ExpectationGroups.Single().Purposes.Single().SumExpectedAmount)
            .ToList();

        expectedAmounts.Count(a => a == -240m).Should().Be(1);
        expectedAmounts[2].Should().Be(-240m);
    }

    [Fact]
    public void SetPlanung_ExpandsCustomMonthsRule_AtConfiguredStep()
    {
        var category = CreateCategory("Subscriptions");
        var purpose = CreatePurpose("Bi-monthly box", BudgetSourceType.Contact, Guid.NewGuid(), category.Id);
        var rule = CreatePurposeRule(purpose.Id, -30m, BudgetIntervalType.CustomMonths, new DateOnly(2026, 1, 1), customIntervalMonths: 2);

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 4, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(new[] { category }, new[] { purpose }, new[] { rule });

        var expectedAmounts = budgetbericht.MonthlyResults
            .Select(m => m.ExpectationGroups.Single().Purposes.Single().SumExpectedAmount)
            .ToList();

        expectedAmounts.Should().Equal(-30m, 0m, -30m, 0m);
    }

    [Fact]
    public void SetPlanung_CreatesExpectationGroup_PerCategory()
    {
        var housing = CreateCategory("Housing");
        var leisure = CreateCategory("Leisure");
        var rentPurpose = CreatePurpose("Rent", BudgetSourceType.Contact, Guid.NewGuid(), housing.Id);
        var cinemaPurpose = CreatePurpose("Cinema", BudgetSourceType.Contact, Guid.NewGuid(), leisure.Id);
        var rules = new[]
        {
            CreatePurposeRule(rentPurpose.Id, -500m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1)),
            CreatePurposeRule(cinemaPurpose.Id, -15m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1))
        };

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(new[] { housing, leisure }, new[] { rentPurpose, cinemaPurpose }, rules);

        var groups = budgetbericht.MonthlyResults.Single().ExpectationGroups;
        groups.Should().HaveCount(2);
        groups.Select(g => g.CategoryName).Should().Contain(new[] { "Housing", "Leisure" });
    }

    [Fact]
    public void SetPlanung_CreatesMultiplePurposeExpectations_ForOneCategory()
    {
        var category = CreateCategory("Shopping & Food");
        var foodPurpose = CreatePurpose("Food", BudgetSourceType.Contact, Guid.NewGuid(), category.Id);
        var bakeryPurpose = CreatePurpose("Bakeries", BudgetSourceType.ContactGroup, Guid.NewGuid(), category.Id);
        var rules = new[]
        {
            CreatePurposeRule(foodPurpose.Id, -300m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1)),
            CreatePurposeRule(bakeryPurpose.Id, -40m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1))
        };

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(new[] { category }, new[] { foodPurpose, bakeryPurpose }, rules);

        var group = budgetbericht.MonthlyResults.Single().ExpectationGroups.Single();
        group.Purposes.Should().HaveCount(2);
        group.Purposes.Select(p => p.Name).Should().Contain(new[] { "Food", "Bakeries" });
    }

    [Fact]
    public void SetPlanung_CreatesUncategorizedVirtualCategory_ForPurposesWithoutCategory()
    {
        var purpose = CreatePurpose("Loose expense", BudgetSourceType.Contact, Guid.NewGuid(), categoryId: null);
        var rule = CreatePurposeRule(purpose.Id, -20m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        var group = budgetbericht.MonthlyResults.Single().ExpectationGroups.Single();
        group.BudgetCategoryId.Should().Be(Guid.Empty);
        group.Purposes.Single().Name.Should().Be("Loose expense");
    }

    [Fact]
    public void SetPlanung_CreatesDirectCategoryExpectation_ForCategoryLevelRule()
    {
        var category = CreateCategory("Housing");
        var rule = CreateCategoryRule(category.Id, -600m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(new[] { category }, Array.Empty<BudgetPurposeDto>(), new[] { rule });

        var group = budgetbericht.MonthlyResults.Single().ExpectationGroups.Single();
        group.DirectExpectations.Single().SumExpectedAmount.Should().Be(-600m);
    }

    [Fact]
    public void SetPlanung_Throws_WhenCalledTwice()
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());

        var act = () => budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());

        act.Should().Throw<BudgetReportCalculationException>();
    }

    [Fact]
    public void SetPlanung_Throws_WhenCustomIntervalRuleHasNoCustomIntervalMonths()
    {
        var purpose = CreatePurpose("Invalid", BudgetSourceType.Contact, Guid.NewGuid());
        var rule = CreatePurposeRule(purpose.Id, -10m, BudgetIntervalType.CustomMonths, new DateOnly(2026, 1, 1), customIntervalMonths: null);

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);

        var act = () => budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        act.Should().Throw<BudgetReportCalculationException>();
    }

    [Fact]
    public void SetPlanung_Throws_WhenRuleIntervalIsInvalid()
    {
        var purpose = CreatePurpose("Invalid", BudgetSourceType.Contact, Guid.NewGuid());
        var rule = CreatePurposeRule(purpose.Id, -10m, (BudgetIntervalType)999, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);

        var act = () => budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        act.Should().Throw<BudgetReportCalculationException>();
    }

    [Fact]
    public void SetPlanung_RuleStartingBeforeReportOrOutsideEndDate_IsExcluded()
    {
        var purpose = CreatePurpose("Ended subscription", BudgetSourceType.Contact, Guid.NewGuid());
        var rule = CreatePurposeRule(purpose.Id, -10m, BudgetIntervalType.Monthly, new DateOnly(2025, 1, 1), endDate: new DateOnly(2025, 6, 30));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        var group = budgetbericht.MonthlyResults.Single().ExpectationGroups.Single();
        group.Purposes.Single().SumExpectedAmount.Should().Be(0m);
    }
}

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
    /// <summary>
    /// Verifies that a monthly-interval rule produces its full amount as an expectation in every month
    /// of the report period - the simplest recurrence case, expanding one rule into N identical monthly
    /// occurrences.
    /// </summary>
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

    /// <summary>
    /// Verifies that a quarterly rule anchored on day 1 produces its amount only in the first month of
    /// each quarter it covers (January and April here), with zero in the other two months of every
    /// quarter - a quarterly occurrence is "homed" to the start of its covering period, not spread or
    /// repeated across all three months.
    /// </summary>
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

        // StartDate is anchored on day 1, so this rule aligns with calendar quarter boundaries and is
        // homed to the period START's month (see ExpandRuleOccurrences) - the Jan-Mar occurrence shows in
        // January (index 0), not spread across the quarter's other months.
        expectedAmounts.Should().Equal(-120m, 0m, 0m, -120m, 0m, 0m);
    }

    /// <summary>
    /// Verifies that a yearly rule with a March start date produces exactly one occurrence across a
    /// full 12-month report, landing in March - the rule's home month tracks its own start date rather
    /// than always defaulting to the report's first month or to January.
    /// </summary>
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

    /// <summary>
    /// Verifies that a <see cref="BudgetIntervalType.CustomMonths"/> rule repeats at the configured
    /// step size (every 2 months here) rather than one of the fixed built-in intervals, and that each
    /// occurrence is homed to the first month of its covering step, mirroring the quarterly homing rule.
    /// </summary>
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

        // StartDate is anchored on day 1, so this rule aligns with calendar boundaries and is homed to the
        // period START's month (see ExpandRuleOccurrences) - the Jan-Feb occurrence shows in January
        // (index 0), the Mar-Apr occurrence in March (index 2).
        expectedAmounts.Should().Equal(-30m, 0m, -30m, 0m);
    }

    /// <summary>
    /// Verifies that purposes belonging to different categories each get their own expectation group
    /// for the month, and that the group carries the category's display name - confirming the
    /// category-level grouping the Output phase later renders as Category rows.
    /// </summary>
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

    /// <summary>
    /// Verifies that multiple purposes sharing the same category are kept as separate purpose entries
    /// within that category's single expectation group, rather than being merged into one combined
    /// purpose expectation.
    /// </summary>
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

    /// <summary>
    /// Verifies that a purpose without a category is still grouped, under a synthetic "uncategorized"
    /// group identified by <see cref="Guid.Empty"/> - every purpose must land in some expectation group
    /// so it isn't silently dropped from planning just because it lacks a category assignment.
    /// </summary>
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

    /// <summary>
    /// Verifies that a rule attached directly to a category (rather than to a purpose) produces a
    /// <c>DirectExpectations</c> entry on that category's group even when the category has no purposes
    /// at all - direct category budgets exist independently of purpose-level budgets.
    /// </summary>
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

    /// <summary>
    /// Verifies that <c>SetPlanung()</c> is a one-shot step, like <c>Finish()</c> - calling it again on
    /// an already-planned report throws <see cref="BudgetReportCalculationException"/> instead of
    /// silently re-expanding rules and potentially duplicating expectations.
    /// </summary>
    [Fact]
    public void SetPlanung_Throws_WhenCalledTwice()
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());

        var act = () => budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());

        act.Should().Throw<BudgetReportCalculationException>();
    }

    /// <summary>
    /// Verifies that a rule declared with <see cref="BudgetIntervalType.CustomMonths"/> but no actual
    /// step size (<c>customIntervalMonths</c> is null) is rejected at planning time rather than causing
    /// an unclear failure or defaulting to some arbitrary interval later during expansion.
    /// </summary>
    [Fact]
    public void SetPlanung_Throws_WhenCustomIntervalRuleHasNoCustomIntervalMonths()
    {
        var purpose = CreatePurpose("Invalid", BudgetSourceType.Contact, Guid.NewGuid());
        var rule = CreatePurposeRule(purpose.Id, -10m, BudgetIntervalType.CustomMonths, new DateOnly(2026, 1, 1), customIntervalMonths: null);

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);

        var act = () => budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        act.Should().Throw<BudgetReportCalculationException>();
    }

    /// <summary>
    /// Verifies that an out-of-range <see cref="BudgetIntervalType"/> value (cast from an arbitrary int,
    /// simulating data corruption or a future enum member not yet handled) is rejected during expansion
    /// rather than falling through an unhandled switch case.
    /// </summary>
    [Fact]
    public void SetPlanung_Throws_WhenRuleIntervalIsInvalid()
    {
        var purpose = CreatePurpose("Invalid", BudgetSourceType.Contact, Guid.NewGuid());
        var rule = CreatePurposeRule(purpose.Id, -10m, (BudgetIntervalType)999, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);

        var act = () => budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        act.Should().Throw<BudgetReportCalculationException>();
    }

    /// <summary>
    /// Verifies that a rule whose <c>EndDate</c> lies entirely before the report's period (a subscription
    /// cancelled in mid-2025, reported on in 2026) produces zero expected amount - an expired rule must
    /// not keep generating occurrences into report periods it no longer applies to.
    /// </summary>
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

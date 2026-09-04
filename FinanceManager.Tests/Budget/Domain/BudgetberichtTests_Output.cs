using FinanceManager.Domain.Budget.ReportCalculation;
using FinanceManager.Shared.Dtos.Budget;
using FluentAssertions;
using Xunit;
using static FinanceManager.Tests.Budget.Domain.BudgetberichtTestFixtures;

namespace FinanceManager.Tests.Budget.Domain;

/// <summary>
/// Tests for <c>Budgetbericht.GetCurrentResult()</c> (Output phase): row-kind structure and aggregation.
/// </summary>
public sealed class BudgetberichtTests_Output
{
    /// <summary>
    /// Verifies the overall row structure produced by <c>GetCurrentResult()</c>: category, purpose and
    /// subtotal rows appear in that order for a categorized purpose, unmatched postings surface as a
    /// distinct Unbudgeted row, self-contact group postings surface as a distinct CostNeutral row, and
    /// exactly one Total row summarizes everything.
    /// </summary>
    [Fact]
    public void GetCurrentResult_ReturnsCategoryPurposeSubtotalUnbudgetedCostNeutralAndTotalRows()
    {
        var category = CreateCategory("Housing");
        var otherCategory = CreateCategory("Leisure");
        var purpose = CreatePurpose("Rent", BudgetSourceType.Contact, Guid.NewGuid(), category.Id);
        var otherPurpose = CreatePurpose("Cinema", BudgetSourceType.Contact, Guid.NewGuid(), otherCategory.Id);
        var rules = new[]
        {
            CreatePurposeRule(purpose.Id, -500m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1)),
            CreatePurposeRule(otherPurpose.Id, -15m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1))
        };

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(new[] { category, otherCategory }, new[] { purpose, otherPurpose }, rules);
        budgetbericht.AddPosting(CreateUnattributedPosting(-9.99m, new DateTime(2026, 1, 12)), BudgetReportDateBasis.BookingDate);
        budgetbericht.AddPosting(CreateUnattributedPosting(3m, new DateTime(2026, 1, 12), groupId: Guid.NewGuid(), isSelfContact: true), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var entries = budgetbericht.GetCurrentResult();

        entries.Select(e => e.RowKind).Should().ContainInOrder(
            BudgetReportEntryRowKind.Category,
            BudgetReportEntryRowKind.Purpose,
            BudgetReportEntryRowKind.Subtotal);
        entries.Should().Contain(e => e.RowKind == BudgetReportEntryRowKind.Unbudgeted && e.ActualAmount == -9.99m);
        entries.Should().Contain(e => e.RowKind == BudgetReportEntryRowKind.CostNeutral && e.ActualAmount == 3m);
        entries.Should().ContainSingle(e => e.RowKind == BudgetReportEntryRowKind.Total);
    }

    /// <summary>
    /// Verifies that each of several distinct categories in use gets its own Category row - the report
    /// does not collapse multiple categories into one summary row.
    /// </summary>
    [Fact]
    public void GetCurrentResult_ShowsCategoryRow_WhenMultipleCategoriesExist()
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
        budgetbericht.Finish();

        var entries = budgetbericht.GetCurrentResult();

        entries.Count(e => e.RowKind == BudgetReportEntryRowKind.Category).Should().Be(2);
    }

    /// <summary>
    /// Verifies that a purpose with no assigned category produces no Category row at all - the report
    /// must not fabricate an empty or "Uncategorized" grouping row when every purpose is uncategorized,
    /// it should just list the purpose directly.
    /// </summary>
    [Fact]
    public void GetCurrentResult_HidesCategoryRow_WhenOnlyUncategorizedPurposesExist()
    {
        var purpose = CreatePurpose("Loose expense", BudgetSourceType.Contact, Guid.NewGuid());
        var rule = CreatePurposeRule(purpose.Id, -20m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });
        budgetbericht.Finish();

        var entries = budgetbericht.GetCurrentResult();

        entries.Should().NotContain(e => e.RowKind == BudgetReportEntryRowKind.Category);
        entries.Should().ContainSingle(e => e.RowKind == BudgetReportEntryRowKind.Purpose && e.Name == "Loose expense");
    }

    /// <summary>
    /// Verifies that a category's row sums both the amounts of the purposes assigned to it and a direct
    /// category-level rule ("Direktes Kategorie-Budget") in the same total - a category can have its own
    /// expectation on top of what its purposes budget individually.
    /// </summary>
    [Fact]
    public void GetCurrentResult_AggregatesCategoryAndPurposeSums_IncludingDirectCategoryExpectations()
    {
        var category = CreateCategory("Housing");
        var otherCategory = CreateCategory("Other");
        var directRule = CreateCategoryRule(category.Id, -100m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));
        var purpose = CreatePurpose("Rent", BudgetSourceType.Contact, Guid.NewGuid(), category.Id);
        var purposeRule = CreatePurposeRule(purpose.Id, -500m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(new[] { category, otherCategory }, new[] { purpose }, new[] { directRule, purposeRule });
        budgetbericht.Finish();

        var categoryRow = budgetbericht.GetCurrentResult().Single(e => e.RowKind == BudgetReportEntryRowKind.Category && e.Name == "Housing");
        categoryRow.BudgetedAmount.Should().Be(-600m);
    }

    /// <summary>
    /// Verifies that passing a specific month to <c>GetCurrentResult(DateOnly)</c> restricts the Total row
    /// to that single month's budgeted amount instead of summing across the whole multi-month report period.
    /// </summary>
    [Fact]
    public void GetCurrentResult_FiltersByMonth_WhenMonthIsGiven()
    {
        var purpose = CreatePurpose("Rent", BudgetSourceType.Contact, Guid.NewGuid());
        var rule = CreatePurposeRule(purpose.Id, -500m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 3, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });
        budgetbericht.Finish();

        var entries = budgetbericht.GetCurrentResult(new DateOnly(2026, 2, 15));

        entries.Single(e => e.RowKind == BudgetReportEntryRowKind.Total).BudgetedAmount.Should().Be(-500m,
            "only February's single month should be included, not all 3 months of the report period");
    }

    /// <summary>
    /// Verifies that omitting the month filter on <c>GetCurrentResult()</c> sums the budgeted amount
    /// across every month of the report period into the Total row - the counterpart to
    /// <see cref="GetCurrentResult_FiltersByMonth_WhenMonthIsGiven"/>.
    /// </summary>
    [Fact]
    public void GetCurrentResult_AggregatesAllMonths_WhenNoMonthIsGiven()
    {
        var purpose = CreatePurpose("Rent", BudgetSourceType.Contact, Guid.NewGuid());
        var rule = CreatePurposeRule(purpose.Id, -500m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 3, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });
        budgetbericht.Finish();

        var entries = budgetbericht.GetCurrentResult();

        entries.Single(e => e.RowKind == BudgetReportEntryRowKind.Total).BudgetedAmount.Should().Be(-1500m);
    }

    /// <summary>
    /// Verifies that a purpose row's <c>Deviation</c> and <c>DeviationPercentage</c> reflect an under-spend
    /// (actual less than budgeted) correctly, deliberately staying within budget so the result isn't
    /// confounded by the overrun-capping behavior covered separately in <c>BudgetberichtTests_Finish</c>.
    /// </summary>
    [Fact]
    public void GetCurrentResult_CalculatesDeviationAndDeviationPercentage()
    {
        // Budgeted -500, actual -450 (under-spent by 50, i.e. 10% of the budgeted amount) -- deliberately
        // not overrunning the budget, since an ExactPostings occurrence caps its own actual amount at its
        // budgeted amount and routes any excess to Unbudgeted (see BudgetberichtTests_Finish).
        var purpose = CreatePurpose("Rent", BudgetSourceType.Contact, Guid.NewGuid());
        var contactId = purpose.SourceId;
        var rule = CreatePurposeRule(purpose.Id, -500m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });
        budgetbericht.AddPosting(CreateContactPosting(-450m, new DateTime(2026, 1, 5), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var purposeRow = budgetbericht.GetCurrentResult().Single(e => e.RowKind == BudgetReportEntryRowKind.Purpose);

        purposeRow.Deviation.Should().Be(50m);
        purposeRow.DeviationPercentage.Should().Be(10m);
    }
}

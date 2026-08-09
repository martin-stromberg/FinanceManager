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

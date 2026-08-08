using FinanceManager.Domain.Budget.ReportCalculation;
using FinanceManager.Infrastructure.Budget.Mapping;
using FinanceManager.Shared.Dtos.Budget;
using FluentAssertions;
using Xunit;
using static FinanceManager.Tests.Budget.Domain.BudgetberichtTestFixtures;

namespace FinanceManager.Tests.Infrastructure.Budget.Mapping;

/// <summary>
/// Tests for <see cref="BudgetberichtMapper.MapToMonthlyKpiDto"/>: planned/actual/expected income and
/// expense aggregation, in particular how cost-neutral postings are (and are not) folded into the
/// resulting KPI figures, and the remaining-planned-amount clamping.
/// </summary>
public sealed class BudgetberichtMapperTests_MonthlyKpi
{
    private static BudgetReportEntry[] BuildEntries(Budgetbericht budgetbericht) => budgetbericht.GetCurrentResult();

    [Fact]
    public void MapToMonthlyKpiDto_ComputesPlannedAndActualIncome_FromMatchingPurpose()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Salary", BudgetSourceType.Contact, contactId, categoryId: null);
        var rule = CreatePurposeRule(purpose.Id, 3000m, BudgetIntervalType.Monthly, new DateOnly(2026, 2, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 2, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });
        budgetbericht.AddPosting(CreateContactPosting(3000m, new DateTime(2026, 2, 25), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var kpi = BudgetberichtMapper.MapToMonthlyKpiDto(BuildEntries(budgetbericht));

        kpi.PlannedIncome.Should().Be(3000m);
        kpi.BudgetedRealizedIncome.Should().Be(3000m);
        kpi.ActualIncome.Should().Be(3000m);
        kpi.ActualResult.Should().Be(3000m);
    }

    [Fact]
    public void MapToMonthlyKpiDto_IncludesCostNeutralPostings_InActualIncomeAndExpense()
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());
        // Self-contact mirror transfers (GroupId set) that match no expectation are cost-neutral, not
        // regular unbudgeted postings, but the "Endsumme"/Total row of GetCurrentResult() still includes
        // them - ActualIncome/ActualExpenseAbs must mirror that.
        budgetbericht.AddPosting(CreateUnattributedPosting(155m, new DateTime(2026, 1, 15), groupId: Guid.NewGuid()), BudgetReportDateBasis.BookingDate);
        budgetbericht.AddPosting(CreateUnattributedPosting(-155m, new DateTime(2026, 1, 15), groupId: Guid.NewGuid()), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var kpi = BudgetberichtMapper.MapToMonthlyKpiDto(BuildEntries(budgetbericht));

        kpi.ActualIncome.Should().Be(155m);
        kpi.ActualExpenseAbs.Should().Be(155m);
        kpi.ActualResult.Should().Be(0m);
    }

    [Fact]
    public void MapToMonthlyKpiDto_ExcludesCostNeutralPostings_FromUnbudgetedAmounts()
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());
        budgetbericht.AddPosting(CreateUnattributedPosting(-9.99m, new DateTime(2026, 1, 12)), BudgetReportDateBasis.BookingDate);
        budgetbericht.AddPosting(CreateUnattributedPosting(3m, new DateTime(2026, 1, 12), groupId: Guid.NewGuid()), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var kpi = BudgetberichtMapper.MapToMonthlyKpiDto(BuildEntries(budgetbericht));

        kpi.UnbudgetedExpenseAbs.Should().Be(9.99m);
        kpi.UnbudgetedIncome.Should().Be(0m);
        // The cost-neutral +3 is part of Actual* but not Unbudgeted*.
        kpi.ActualIncome.Should().Be(3m);
    }

    [Fact]
    public void MapToMonthlyKpiDto_ComputesRemainingAndExpectedAmounts_WhenActualBelowPlanned()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Rent", BudgetSourceType.Contact, contactId, categoryId: null);
        var rule = CreatePurposeRule(purpose.Id, -500m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });
        budgetbericht.AddPosting(CreateContactPosting(-300m, new DateTime(2026, 1, 5), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var kpi = BudgetberichtMapper.MapToMonthlyKpiDto(BuildEntries(budgetbericht));

        kpi.RemainingPlannedExpenseAbs.Should().Be(200m);
        kpi.ExpectedExpenseAbs.Should().Be(500m);
        kpi.ExpectedTargetResult.Should().Be(-500m);
    }

    [Fact]
    public void MapToMonthlyKpiDto_RemainingPlannedExpenseAbs_ClampsToZero_WhenActualExceedsPlanned()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Groceries", BudgetSourceType.Contact, contactId, categoryId: null, valuationType: BudgetValuationType.TotalBudget);
        var rule = CreatePurposeRule(purpose.Id, -100m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });
        budgetbericht.AddPosting(CreateContactPosting(-140m, new DateTime(2026, 1, 5), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var kpi = BudgetberichtMapper.MapToMonthlyKpiDto(BuildEntries(budgetbericht));

        kpi.RemainingPlannedExpenseAbs.Should().Be(0m);
        kpi.BudgetedRealizedExpenseAbs.Should().Be(100m);
        kpi.UnbudgetedExpenseAbs.Should().Be(40m);
    }

    [Fact]
    public void MapToMonthlyKpiDto_ThrowsArgumentNullException_WhenEntriesIsNull()
    {
        var act = () => BudgetberichtMapper.MapToMonthlyKpiDto(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

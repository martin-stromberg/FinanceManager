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

    /// <summary>
    /// Verifies the baseline case: a posting that exactly matches its budgeted purpose's rule is reflected
    /// consistently in planned income, budgeted-realized income, actual income, and the actual result.
    /// </summary>
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

    /// <summary>
    /// Verifies that cost-neutral self-contact mirror transfers (postings whose <c>GroupId</c> marks them as offsetting
    /// pairs, not real income/expense) are still counted in <c>ActualIncome</c>/<c>ActualExpenseAbs</c>, matching the
    /// "Endsumme" total row produced by <c>Budgetbericht.GetCurrentResult()</c> - the KPI mapper must not silently
    /// diverge from that total.
    /// </summary>
    [Fact]
    public void MapToMonthlyKpiDto_IncludesCostNeutralPostings_InActualIncomeAndExpense()
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());
        // Self-contact mirror transfers (GroupId set) that match no expectation are cost-neutral, not
        // regular unbudgeted postings, but the "Endsumme"/Total row of GetCurrentResult() still includes
        // them - ActualIncome/ActualExpenseAbs must mirror that.
        budgetbericht.AddPosting(CreateUnattributedPosting(155m, new DateTime(2026, 1, 15), groupId: Guid.NewGuid(), isSelfContact: true), BudgetReportDateBasis.BookingDate);
        budgetbericht.AddPosting(CreateUnattributedPosting(-155m, new DateTime(2026, 1, 15), groupId: Guid.NewGuid(), isSelfContact: true), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var kpi = BudgetberichtMapper.MapToMonthlyKpiDto(BuildEntries(budgetbericht));

        kpi.ActualIncome.Should().Be(155m);
        kpi.ActualExpenseAbs.Should().Be(155m);
        kpi.ActualResult.Should().Be(0m);
    }

    /// <summary>
    /// Verifies the flip side of the cost-neutral handling: while cost-neutral postings count toward
    /// <c>Actual*</c>, they must be excluded from <c>Unbudgeted*</c> - otherwise a self-transfer would be
    /// misreported to the user as an unplanned expense or income.
    /// </summary>
    [Fact]
    public void MapToMonthlyKpiDto_ExcludesCostNeutralPostings_FromUnbudgetedAmounts()
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());
        budgetbericht.AddPosting(CreateUnattributedPosting(-9.99m, new DateTime(2026, 1, 12)), BudgetReportDateBasis.BookingDate);
        budgetbericht.AddPosting(CreateUnattributedPosting(3m, new DateTime(2026, 1, 12), groupId: Guid.NewGuid(), isSelfContact: true), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var kpi = BudgetberichtMapper.MapToMonthlyKpiDto(BuildEntries(budgetbericht));

        kpi.UnbudgetedExpenseAbs.Should().Be(9.99m);
        kpi.UnbudgetedIncome.Should().Be(0m);
        // The cost-neutral +3 is part of Actual* but not Unbudgeted*.
        kpi.ActualIncome.Should().Be(3m);
    }

    /// <summary>
    /// Verifies that when actual spending is below the planned amount for an expense purpose, the mapper computes
    /// the remaining planned budget, the expected end-of-period expense (which projects the still-unspent portion),
    /// and the expected target result consistently from the same rule.
    /// </summary>
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

    /// <summary>
    /// Verifies that when actual spending overruns a purpose's planned budget, <c>RemainingPlannedExpenseAbs</c>
    /// clamps to zero instead of going negative, and that the overrun stays attributed to the specific purpose
    /// rather than leaking into <c>UnbudgetedExpenseAbs</c> - a negative "remaining" figure would be misleading in
    /// the UI, and misattributing the overrun would make the unbudgeted total wrong.
    /// </summary>
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
        // The 40 overrun stays attributed to the "Groceries" purpose (not the generic Unbudgeted row),
        // so it does not show up as UnbudgetedExpenseAbs here.
        kpi.UnbudgetedExpenseAbs.Should().Be(0m);
    }

    /// <summary>
    /// Tests that income-side remaining planned and expected amounts are computed correctly
    /// when actual income is below planned income (analogous to the expense-side test).
    /// </summary>
    [Fact]
    public void MapToMonthlyKpiDto_ComputesRemainingAndExpectedIncome_WhenActualBelowPlanned()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Freelance", BudgetSourceType.Contact, contactId, categoryId: null);
        var rule = CreatePurposeRule(purpose.Id, 2000m, BudgetIntervalType.Monthly, new DateOnly(2026, 3, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 3, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });
        budgetbericht.AddPosting(CreateContactPosting(1200m, new DateTime(2026, 3, 15), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var kpi = BudgetberichtMapper.MapToMonthlyKpiDto(BuildEntries(budgetbericht));

        kpi.PlannedIncome.Should().Be(2000m);
        kpi.BudgetedRealizedIncome.Should().Be(1200m);
        kpi.RemainingPlannedIncome.Should().Be(800m);
        kpi.ActualIncome.Should().Be(1200m);
        kpi.ExpectedIncome.Should().Be(2000m);
        kpi.ExpectedTargetResult.Should().Be(2000m);
    }

    /// <summary>
    /// Verifies that passing a null entries array fails fast with <see cref="ArgumentNullException"/> instead of a
    /// later <see cref="NullReferenceException"/> deep inside the aggregation logic.
    /// </summary>
    [Fact]
    public void MapToMonthlyKpiDto_ThrowsArgumentNullException_WhenEntriesIsNull()
    {
        var act = () => BudgetberichtMapper.MapToMonthlyKpiDto(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

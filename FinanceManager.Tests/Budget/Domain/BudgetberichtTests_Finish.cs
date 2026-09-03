using FinanceManager.Domain.Budget.ReportCalculation;
using FinanceManager.Shared.Dtos.Budget;
using FluentAssertions;
using Xunit;
using static FinanceManager.Tests.Budget.Domain.BudgetberichtTestFixtures;

namespace FinanceManager.Tests.Budget.Domain;

/// <summary>
/// Tests for <c>Budgetbericht.Finish()</c>: overrun splitting and multi-occurrence reconciliation.
/// </summary>
public sealed class BudgetberichtTests_Finish
{
    /// <summary>
    /// Verifies that when actual postings exceed a fixed ("TotalBudget") expectation, the expectation's
    /// counted amount caps at the budgeted value (so <c>Variance</c> stays zero) while the overrun is
    /// still attributed to the same purpose as an unvalued matched amount - not dumped into the generic
    /// unbudgeted or cost-neutral buckets, which are reserved for postings matching no purpose at all.
    /// </summary>
    [Fact]
    public void Finish_SplitsOverflowIntoUnvaluedAtPurpose_ForExactPostingExpectation()
    {
        // "Streaming Provider" scenario: expectation -10, actual postings -4.99/-4.99/-6.00 (overrun of -6.98).
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Streaming Provider", BudgetSourceType.Contact, contactId, valuationType: BudgetValuationType.TotalBudget);
        var rule = CreatePurposeRule(purpose.Id, -10m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        budgetbericht.AddPosting(CreateContactPosting(-4.99m, new DateTime(2026, 1, 8), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.AddPosting(CreateContactPosting(-4.99m, new DateTime(2026, 1, 8), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.AddPosting(CreateContactPosting(-6.00m, new DateTime(2026, 1, 8), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var monthResult = budgetbericht.MonthlyResults.Single();
        var expectation = monthResult.ExpectationGroups.Single().Purposes.Single();

        expectation.SumActualAmount.Should().Be(-10m, "the expectation caps at its own budgeted amount");
        expectation.Variance.Should().Be(0m);
        // The overrun beyond the -10 budget still belongs to this purpose (not the generic
        // Unbudgeted/CostNeutral buckets, which are reserved for postings matching no purpose at all).
        expectation.Postings.SelectMany(p => p.UnvaluedMatchedPostings).Sum(p => p.Amount)
            .Should().Be(-5.98m, "the overrun beyond the -10 budget must stay attributed to this purpose");
        monthResult.UnbudgetedPostings.Should().BeEmpty();
        monthResult.CostNeutralPostings.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that when a purpose has two overlapping monthly rules (two separate -5 budget occurrences),
    /// their expected amounts are combined into a single -10 expectation for the month, actual postings
    /// are reconciled against that combined total, and only the amount beyond the combined budget is
    /// left unvalued at the purpose.
    /// </summary>
    [Fact]
    public void Finish_CombinesMultipleBudgetsPerPurpose_AndMarksExcessUnvaluedAtPurpose()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Combined budget", BudgetSourceType.Contact, contactId, valuationType: BudgetValuationType.TotalBudget);
        var firstRule = CreatePurposeRule(purpose.Id, -5m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));
        // StartDate is a priority tie-breaker, but also anchors the occurrence's own eligibility period
        // (see AddPosting_MultipleTotalBudgets_AssignsToEarliestStartDateFirst) -- keep it before the
        // posting date so both occurrences are actually eligible for it. Anchored on day 1 (like
        // firstRule), just a month earlier, so its generated January occurrence's period stays entirely
        // within January (day != 1 would make the period end in February, homing its budgeted amount
        // there instead - see ExpandRuleOccurrences - which is not what this test is about).
        var secondRule = CreatePurposeRule(purpose.Id, -5m, BudgetIntervalType.Monthly, new DateOnly(2025, 12, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { firstRule, secondRule });

        budgetbericht.AddPosting(CreateContactPosting(-12m, new DateTime(2026, 1, 5), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var monthResult = budgetbericht.MonthlyResults.Single();
        var expectation = monthResult.ExpectationGroups.Single().Purposes.Single();

        expectation.SumExpectedAmount.Should().Be(-10m);
        expectation.SumActualAmount.Should().Be(-10m);
        expectation.Postings.SelectMany(p => p.UnvaluedMatchedPostings).Sum(p => p.Amount).Should().Be(-2m);
        monthResult.UnbudgetedPostings.Should().BeEmpty();
        monthResult.CostNeutralPostings.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that <c>Finish()</c> reconciles postings against rule occurrences in posting-date order,
    /// not in the order <c>AddPosting</c> was called - the later posting is added first here, yet the
    /// earliest-priority occurrence still ends up assigned the earliest-dated posting.
    /// </summary>
    [Fact]
    public void Finish_ReassignsPostingsInPostingDateOrder_RegardlessOfAddPostingCallOrder()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Reordered", BudgetSourceType.Contact, contactId, valuationType: BudgetValuationType.TotalBudget);
        var firstRule = CreatePurposeRule(purpose.Id, -10m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));
        var secondRule = CreatePurposeRule(purpose.Id, -10m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 2));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { firstRule, secondRule });

        // Add the later posting first, to verify Finish() re-sorts by posting date, not call order.
        budgetbericht.AddPosting(CreateContactPosting(-10m, new DateTime(2026, 1, 20), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.AddPosting(CreateContactPosting(-10m, new DateTime(2026, 1, 3), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var occurrences = budgetbericht.MonthlyResults.Single().ExpectationGroups.Single().Purposes.Single().Postings;
        var earlyOccurrence = occurrences.Single(o => o.StartDate == new DateOnly(2026, 1, 1));

        earlyOccurrence.AssignedPostings.Should().ContainSingle(p => p.BookingDate == new DateTime(2026, 1, 3),
            "the earliest posting-date posting must be assigned to the earliest-priority occurrence after reconciliation");
    }

    /// <summary>
    /// Verifies that overrun handling for an "ExactPostings" income expectation mirrors the expense case:
    /// a salary posting that exceeds the expected 3000 still caps the counted actual amount at 3000, with
    /// the surplus 200 left unvalued at the purpose rather than inflating the expectation's actual amount.
    /// </summary>
    [Fact]
    public void Finish_IncomeExpectation_OverrunStaysUnvaluedAtPurpose()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Salary", BudgetSourceType.Contact, contactId, valuationType: BudgetValuationType.ExactPostings);
        var rule = CreatePurposeRule(purpose.Id, 3000m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        budgetbericht.AddPosting(CreateContactPosting(3200m, new DateTime(2026, 1, 25), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var monthResult = budgetbericht.MonthlyResults.Single();
        var expectation = monthResult.ExpectationGroups.Single().Purposes.Single();

        expectation.SumActualAmount.Should().Be(3000m);
        expectation.Postings.SelectMany(p => p.UnvaluedMatchedPostings).Sum(p => p.Amount).Should().Be(200m);
        monthResult.UnbudgetedPostings.Should().BeEmpty();
        monthResult.CostNeutralPostings.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that <c>Finish()</c> is a one-shot finalization step - calling it a second time on an
    /// already-finished report throws <see cref="BudgetReportCalculationException"/> instead of silently
    /// re-running (and potentially double-counting) the reconciliation.
    /// </summary>
    [Fact]
    public void Finish_Throws_WhenCalledTwice()
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());
        budgetbericht.Finish();

        var act = () => budgetbericht.Finish();

        act.Should().Throw<BudgetReportCalculationException>();
    }
}

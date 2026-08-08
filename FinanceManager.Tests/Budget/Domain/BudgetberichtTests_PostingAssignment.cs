using FinanceManager.Domain.Budget.ReportCalculation;
using FinanceManager.Shared.Dtos.Budget;
using FluentAssertions;
using Xunit;
using static FinanceManager.Tests.Budget.Domain.BudgetberichtTestFixtures;

namespace FinanceManager.Tests.Budget.Domain;

/// <summary>
/// Tests for <c>Budgetbericht.AddPosting()</c> (Posting Assignment phase): source/pattern/sign matching
/// and priority handling.
/// </summary>
public sealed class BudgetberichtTests_PostingAssignment
{
    private static Budgetbericht CreatePlanned(BudgetCategoryDto[] categories, BudgetPurposeDto[] purposes, BudgetRuleDto[] rules, int months = 1)
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), months, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(categories, purposes, rules);
        return budgetbericht;
    }

    [Fact]
    public void AddPosting_AssignsPosting_ToMatchingContactPurpose()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Rent", BudgetSourceType.Contact, contactId);
        var rule = CreatePurposeRule(purpose.Id, -500m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        var posting = CreateContactPosting(-500m, new DateTime(2026, 1, 10), contactId);
        budgetbericht.AddPosting(posting, BudgetReportDateBasis.BookingDate);

        var expectation = budgetbericht.MonthlyResults.Single().ExpectationGroups.Single().Purposes.Single();
        expectation.SumActualAmount.Should().Be(-500m);
    }

    [Fact]
    public void AddPosting_AssignsPosting_ToMatchingContactGroupPurpose()
    {
        var groupId = Guid.NewGuid();
        var purpose = CreatePurpose("Bakeries", BudgetSourceType.ContactGroup, groupId);
        var rule = CreatePurposeRule(purpose.Id, -40m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        var posting = CreateContactPosting(-12.50m, new DateTime(2026, 1, 5), Guid.NewGuid(), contactGroupId: groupId);
        budgetbericht.AddPosting(posting, BudgetReportDateBasis.BookingDate);

        var expectation = budgetbericht.MonthlyResults.Single().ExpectationGroups.Single().Purposes.Single();
        expectation.SumActualAmount.Should().Be(-12.50m);
    }

    [Fact]
    public void AddPosting_AssignsPosting_ToMatchingSavingsPlanPurpose()
    {
        var savingsPlanId = Guid.NewGuid();
        var purpose = CreatePurpose("Insurance reserve", BudgetSourceType.SavingsPlan, savingsPlanId);
        var rule = CreatePurposeRule(purpose.Id, -5m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        var posting = CreateSavingsPlanPosting(-5m, new DateTime(2026, 1, 10), savingsPlanId);
        budgetbericht.AddPosting(posting, BudgetReportDateBasis.BookingDate);

        var expectation = budgetbericht.MonthlyResults.Single().ExpectationGroups.Single().Purposes.Single();
        expectation.SumActualAmount.Should().Be(-5m);
    }

    [Fact]
    public void AddPosting_AppliesSubstringPurposePattern()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Electricity", BudgetSourceType.Contact, contactId);
        var rule = CreatePurposeRule(purpose.Id, -80m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1), purposePattern: "KNR-4711");
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        var matching = CreateContactPosting(-80m, new DateTime(2026, 1, 5), contactId, purpose: "Abrechnung KNR-4711");
        var nonMatching = CreateContactPosting(-49.90m, new DateTime(2026, 1, 15), contactId, purpose: "Verkehrsabo VABO-9000");
        budgetbericht.AddPosting(matching, BudgetReportDateBasis.BookingDate);
        budgetbericht.AddPosting(nonMatching, BudgetReportDateBasis.BookingDate);

        var monthResult = budgetbericht.MonthlyResults.Single();
        monthResult.ExpectationGroups.Single().Purposes.Single().SumActualAmount.Should().Be(-80m);
        monthResult.UnbudgetedPostings.Should().ContainSingle(p => p.Amount == -49.90m);
    }

    [Fact]
    public void AddPosting_AppliesRegexPurposePattern()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Utilities", BudgetSourceType.Contact, contactId);
        var rule = CreatePurposeRule(purpose.Id, -60m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1), purposePattern: "ST\\d{10}", useRegex: true);
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        var matching = CreateContactPosting(-60m, new DateTime(2026, 1, 5), contactId, purpose: "Abrechnung ST6464646464 Januar");
        var nonMatching = CreateContactPosting(-40m, new DateTime(2026, 1, 6), contactId, purpose: "Service ohne Vertragsnummer");
        budgetbericht.AddPosting(matching, BudgetReportDateBasis.BookingDate);
        budgetbericht.AddPosting(nonMatching, BudgetReportDateBasis.BookingDate);

        var monthResult = budgetbericht.MonthlyResults.Single();
        monthResult.ExpectationGroups.Single().Purposes.Single().SumActualAmount.Should().Be(-60m);
        monthResult.UnbudgetedPostings.Should().ContainSingle(p => p.Amount == -40m);
    }

    [Fact]
    public void AddPosting_ExactPostings_RequiresMatchingSign()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Membership", BudgetSourceType.Contact, contactId, valuationType: BudgetValuationType.ExactPostings);
        var rule = CreatePurposeRule(purpose.Id, -15m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1), purposePattern: "VEREIN");
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        var expense = CreateContactPosting(-12.50m, new DateTime(2026, 1, 3), contactId, purpose: "VEREIN Beitrag");
        var refund = CreateContactPosting(9.40m, new DateTime(2026, 1, 4), contactId, purpose: "VEREIN Erstattung");
        budgetbericht.AddPosting(expense, BudgetReportDateBasis.BookingDate);
        budgetbericht.AddPosting(refund, BudgetReportDateBasis.BookingDate);

        var monthResult = budgetbericht.MonthlyResults.Single();
        var expectation = monthResult.ExpectationGroups.Single().Purposes.Single();
        expectation.SumActualAmount.Should().Be(-12.50m, "the refund has the wrong sign for an ExactPostings expectation");
        monthResult.UnbudgetedPostings.Should().ContainSingle(p => p.Amount == 9.40m);
    }

    [Fact]
    public void AddPosting_ExactPostings_SignMismatch_RecordsPostingAsUnvaluedMatchOnTheOccurrence()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Membership", BudgetSourceType.Contact, contactId, valuationType: BudgetValuationType.ExactPostings);
        var rule = CreatePurposeRule(purpose.Id, -15m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1), purposePattern: "VEREIN");
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        var refund = CreateContactPosting(9.40m, new DateTime(2026, 1, 4), contactId, purpose: "VEREIN Erstattung");
        budgetbericht.AddPosting(refund, BudgetReportDateBasis.BookingDate);

        var occurrence = budgetbericht.MonthlyResults.Single().ExpectationGroups.Single().Purposes.Single().Postings.Single();
        occurrence.UnvaluedMatchedPostings.Should().ContainSingle(p => p.Amount == 9.40m,
            "a posting that matches the purpose's source and pattern, but not its sign, is still visible against the occurrence, just not valued");
        occurrence.AssignedPostings.Should().BeEmpty();
    }

    [Fact]
    public void AddPosting_TotalBudget_AcceptsAnySign()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Cashback account", BudgetSourceType.Contact, contactId, valuationType: BudgetValuationType.TotalBudget);
        var rule = CreatePurposeRule(purpose.Id, -50m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        var expense = CreateContactPosting(-30m, new DateTime(2026, 1, 3), contactId);
        var income = CreateContactPosting(5m, new DateTime(2026, 1, 4), contactId);
        budgetbericht.AddPosting(expense, BudgetReportDateBasis.BookingDate);
        budgetbericht.AddPosting(income, BudgetReportDateBasis.BookingDate);

        var expectation = budgetbericht.MonthlyResults.Single().ExpectationGroups.Single().Purposes.Single();
        expectation.SumActualAmount.Should().Be(-25m);
    }

    [Fact]
    public void AddPosting_MultipleTotalBudgets_AssignsToEarliestStartDateFirst()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Streaming", BudgetSourceType.Contact, contactId, valuationType: BudgetValuationType.TotalBudget);
        var earlyRule = CreatePurposeRule(purpose.Id, -5m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));
        // StartDate must still be before the posting date, so both occurrences' periods actually cover it
        // (an occurrence's period starts at its rule's StartDate) -- otherwise the "late" occurrence would
        // simply be ineligible for this posting rather than losing on priority.
        var lateRule = CreatePurposeRule(purpose.Id, -5m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 2));
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { earlyRule, lateRule });

        var posting = CreateContactPosting(-8m, new DateTime(2026, 1, 5), contactId);
        budgetbericht.AddPosting(posting, BudgetReportDateBasis.BookingDate);

        var occurrences = budgetbericht.MonthlyResults.Single().ExpectationGroups.Single().Purposes.Single().Postings;
        var earlyOccurrence = occurrences.Single(o => o.StartDate == new DateOnly(2026, 1, 1));
        var lateOccurrence = occurrences.Single(o => o.StartDate == new DateOnly(2026, 1, 2));

        earlyOccurrence.SumAssignedAmount.Should().Be(-5m, "the earliest StartDate has priority and absorbs its full capacity first");
        lateOccurrence.SumAssignedAmount.Should().Be(-3m, "the remaining amount overflows to the next occurrence");
    }

    [Fact]
    public void AddPosting_NoMatch_RoutesToUnbudgeted()
    {
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());

        var posting = CreateUnattributedPosting(-49.90m, new DateTime(2026, 1, 15), purpose: "Verkehrsabo VABO-9000");
        budgetbericht.AddPosting(posting, BudgetReportDateBasis.BookingDate);

        budgetbericht.MonthlyResults.Single().UnbudgetedPostings.Should().ContainSingle(p => p.Amount == -49.90m);
        budgetbericht.MonthlyResults.Single().CostNeutralPostings.Should().BeEmpty();
    }

    [Fact]
    public void AddPosting_NoMatchWithGroupId_RoutesToCostNeutral()
    {
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());

        var posting = CreateUnattributedPosting(12.34m, new DateTime(2026, 1, 27), purpose: "Extra", groupId: Guid.NewGuid());
        budgetbericht.AddPosting(posting, BudgetReportDateBasis.BookingDate);

        budgetbericht.MonthlyResults.Single().CostNeutralPostings.Should().ContainSingle(p => p.Amount == 12.34m);
        budgetbericht.MonthlyResults.Single().UnbudgetedPostings.Should().BeEmpty();
    }

    [Fact]
    public void AddPosting_OutsideReportPeriod_IsIgnored()
    {
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());

        var posting = CreateUnattributedPosting(-10m, new DateTime(2025, 6, 1));
        budgetbericht.AddPosting(posting, BudgetReportDateBasis.BookingDate);

        budgetbericht.MonthlyResults.Single().UnbudgetedPostings.Should().BeEmpty();
    }

    [Fact]
    public void AddPosting_UsesValutaDate_WhenDateBasisIsValutaDate()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Dividend", BudgetSourceType.Contact, contactId);
        var rule = CreatePurposeRule(purpose.Id, 10m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 2, BudgetReportInterval.Month, BudgetReportDateBasis.ValutaDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        // Booked in December, valued in January -> must land in the January MonthlyBudgetResult.
        var posting = CreateContactPosting(10m, new DateTime(2025, 12, 30), contactId, valutaDate: new DateTime(2026, 1, 2));
        budgetbericht.AddPosting(posting, BudgetReportDateBasis.ValutaDate);

        var januaryResult = budgetbericht.MonthlyResults.First(m => m.Month == new DateTime(2026, 1, 1));
        januaryResult.ExpectationGroups.Single().Purposes.Single().SumActualAmount.Should().Be(10m);
    }

    [Fact]
    public void AddPosting_Throws_WhenPostingIsNull()
    {
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());

        var act = () => budgetbericht.AddPosting(null!, BudgetReportDateBasis.BookingDate);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddPosting_Throws_AfterFinishHasBeenCalled()
    {
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());
        budgetbericht.Finish();

        var posting = CreateUnattributedPosting(-1m, new DateTime(2026, 1, 5));
        var act = () => budgetbericht.AddPosting(posting, BudgetReportDateBasis.BookingDate);

        act.Should().Throw<BudgetReportCalculationException>();
    }
}

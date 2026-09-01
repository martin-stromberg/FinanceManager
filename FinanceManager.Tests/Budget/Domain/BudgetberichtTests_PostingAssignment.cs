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

    /// <summary>
    /// Verifies the baseline case: a posting attributed to the exact contact a purpose targets
    /// (<see cref="BudgetSourceType.Contact"/>) is matched and counted toward that purpose's actual amount.
    /// </summary>
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

    /// <summary>
    /// Verifies that a purpose sourced from a <see cref="BudgetSourceType.ContactGroup"/> matches postings
    /// by the posting's <c>ContactGroupId</c> rather than requiring a specific contact - any member of the
    /// group should route to the purpose.
    /// </summary>
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

    /// <summary>
    /// Verifies that a purpose sourced from a <see cref="BudgetSourceType.SavingsPlan"/> matches postings
    /// by <c>SavingsPlanId</c> - the third of the three source-matching strategies (contact, contact
    /// group, savings plan) that <c>AddPosting</c> supports.
    /// </summary>
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

    /// <summary>
    /// Verifies that a rule with a plain-text purpose pattern (matched as a substring of the posting's
    /// purpose/subject text) accepts a posting whose text contains it and routes a same-source posting
    /// with unrelated text to Unbudgeted instead - source matching alone is not sufficient once a pattern
    /// is configured on the rule.
    /// </summary>
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

    /// <summary>
    /// Verifies the regex counterpart of the substring pattern match: when <c>UseRegex</c> is set, the
    /// purpose pattern is evaluated as a regular expression against the posting text (matching a
    /// variable-length contract number here) rather than as a literal substring.
    /// </summary>
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

    /// <summary>
    /// Verifies that a purpose valued as <see cref="BudgetValuationType.ExactPostings"/> only counts
    /// postings whose sign matches the expectation's sign (an expense purpose only counts outgoing
    /// postings) - a same-source, same-pattern refund with the opposite sign is excluded from the actual
    /// amount, even though it is still visible on the occurrence as an unvalued match rather than
    /// disappearing into the report's generic Unbudgeted bucket.
    /// </summary>
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
        // The sign-mismatched refund is shown against this purpose (see
        // AddPosting_ExactPostings_SignMismatch_RecordsPostingAsUnvaluedMatchOnTheOccurrence) and must
        // therefore NOT also appear in the month's top-level Unbudgeted bucket.
        expectation.Postings.SelectMany(p => p.UnvaluedMatchedPostings).Should().ContainSingle(p => p.Amount == 9.40m);
        monthResult.UnbudgetedPostings.Should().BeEmpty();
    }

    /// <summary>
    /// Drills into the sign-mismatch case in isolation: a sign-mismatched posting is recorded on the
    /// matching occurrence's <c>UnvaluedMatchedPostings</c> (not its <c>AssignedPostings</c>, which stays
    /// empty), and it must not also leak into the month's top-level Unbudgeted or CostNeutral buckets -
    /// a posting that matched a purpose belongs to that purpose's view, valued or not.
    /// </summary>
    [Fact]
    public void AddPosting_ExactPostings_SignMismatch_RecordsPostingAsUnvaluedMatchOnTheOccurrence()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Membership", BudgetSourceType.Contact, contactId, valuationType: BudgetValuationType.ExactPostings);
        var rule = CreatePurposeRule(purpose.Id, -15m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1), purposePattern: "VEREIN");
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });

        var refund = CreateContactPosting(9.40m, new DateTime(2026, 1, 4), contactId, purpose: "VEREIN Erstattung");
        budgetbericht.AddPosting(refund, BudgetReportDateBasis.BookingDate);

        var monthResult = budgetbericht.MonthlyResults.Single();
        var occurrence = monthResult.ExpectationGroups.Single().Purposes.Single().Postings.Single();
        occurrence.UnvaluedMatchedPostings.Should().ContainSingle(p => p.Amount == 9.40m,
            "a posting that matches the purpose's source and pattern, but not its sign, is still visible against the occurrence, just not valued");
        occurrence.AssignedPostings.Should().BeEmpty();
        // A posting shown at its purpose must not also be listed as (self-contact) unbudgeted/cost-neutral.
        monthResult.UnbudgetedPostings.Should().BeEmpty();
        monthResult.CostNeutralPostings.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that, unlike <see cref="BudgetValuationType.ExactPostings"/>, a
    /// <see cref="BudgetValuationType.TotalBudget"/> purpose nets postings of either sign into the actual
    /// amount (e.g. a cashback account with both spending and refunds) - the sign-matching restriction is
    /// specific to the ExactPostings valuation, not a rule shared by all purposes.
    /// </summary>
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

    /// <summary>
    /// Verifies the priority order used to reconcile a posting against multiple eligible occurrences of
    /// the same purpose: the occurrence with the earliest rule <c>StartDate</c> absorbs the posting first,
    /// up to its own capacity, and only the remainder overflows to the next occurrence in priority order -
    /// the same overflow mechanism <c>BudgetberichtTests_Finish</c> exercises at the purpose-total level,
    /// observed here at the individual-occurrence level via <c>SumAssignedAmount</c>.
    /// </summary>
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

    /// <summary>
    /// Verifies that a posting matching no purpose or category by any of the source strategies falls
    /// back to the month's Unbudgeted bucket and not CostNeutral - the default outcome for genuinely
    /// unclassified spending/income.
    /// </summary>
    [Fact]
    public void AddPosting_NoMatch_RoutesToUnbudgeted()
    {
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());

        var posting = CreateUnattributedPosting(-49.90m, new DateTime(2026, 1, 15), purpose: "Verkehrsabo VABO-9000");
        budgetbericht.AddPosting(posting, BudgetReportDateBasis.BookingDate);

        budgetbericht.MonthlyResults.Single().UnbudgetedPostings.Should().ContainSingle(p => p.Amount == -49.90m);
        budgetbericht.MonthlyResults.Single().CostNeutralPostings.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that an unmatched posting is only routed to CostNeutral (rather than Unbudgeted) when it
    /// is both grouped (<c>GroupId</c> set, linking it to its paired ledger leg) and attributed to the
    /// owner's own "Self" contact - the specific combination that identifies a cost-neutral internal
    /// transfer mirror leg.
    /// </summary>
    [Fact]
    public void AddPosting_NoMatchWithGroupIdAndSelfContact_RoutesToCostNeutral()
    {
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());

        var posting = CreateUnattributedPosting(12.34m, new DateTime(2026, 1, 27), purpose: "Extra", groupId: Guid.NewGuid(), isSelfContact: true);
        budgetbericht.AddPosting(posting, BudgetReportDateBasis.BookingDate);

        budgetbericht.MonthlyResults.Single().CostNeutralPostings.Should().ContainSingle(p => p.Amount == 12.34m);
        budgetbericht.MonthlyResults.Single().UnbudgetedPostings.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that <c>GroupId</c> alone is not sufficient to classify a posting as cost-neutral - since
    /// nearly every booked posting carries a <c>GroupId</c> linking it to its paired ledger leg, a grouped
    /// posting NOT attributed to the Self contact (e.g. an ordinary external payment) must still fall back
    /// to Unbudgeted, guarding against over-broadly treating grouped postings as internal transfers.
    /// </summary>
    [Fact]
    public void AddPosting_NoMatchWithGroupId_ButNotSelfContact_RoutesToUnbudgeted()
    {
        // GroupId links a posting to its paired ledger leg (e.g. the bank-side and contact-side leg of the
        // same booked transaction) and is set for essentially every booked posting - it does not, on its
        // own, identify a cost-neutral self-contact mirror transfer. A grouped posting that is NOT attributed
        // to the Self contact (e.g. an ordinary external payment) must still be routed to Unbudgeted.
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());

        var posting = CreateUnattributedPosting(8.37m, new DateTime(2026, 1, 27), purpose: "Dividend", groupId: Guid.NewGuid(), isSelfContact: false);
        budgetbericht.AddPosting(posting, BudgetReportDateBasis.BookingDate);

        budgetbericht.MonthlyResults.Single().UnbudgetedPostings.Should().ContainSingle(p => p.Amount == 8.37m);
        budgetbericht.MonthlyResults.Single().CostNeutralPostings.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that a posting whose date falls entirely outside the report's month range is silently
    /// dropped rather than being force-fit into the nearest bucket or throwing - the report only reflects
    /// activity within its own configured period.
    /// </summary>
    [Fact]
    public void AddPosting_OutsideReportPeriod_IsIgnored()
    {
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());

        var posting = CreateUnattributedPosting(-10m, new DateTime(2025, 6, 1));
        budgetbericht.AddPosting(posting, BudgetReportDateBasis.BookingDate);

        budgetbericht.MonthlyResults.Single().UnbudgetedPostings.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that when the report is configured with <see cref="BudgetReportDateBasis.ValutaDate"/>,
    /// a posting is bucketed by its value date rather than its booking date - a posting booked in
    /// December but valued in January must land in the January <c>MonthlyResult</c>, matching how banks
    /// often settle transactions a few days after the booking date.
    /// </summary>
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

    /// <summary>
    /// Verifies that <c>AddPosting</c> guards against a null posting argument with
    /// <see cref="ArgumentNullException"/> rather than throwing a less diagnostic
    /// <see cref="NullReferenceException"/> once it starts reading the posting's properties.
    /// </summary>
    [Fact]
    public void AddPosting_Throws_WhenPostingIsNull()
    {
        var budgetbericht = CreatePlanned(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());

        var act = () => budgetbericht.AddPosting(null!, BudgetReportDateBasis.BookingDate);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that once <c>Finish()</c> has finalized the report, <c>AddPosting</c> is rejected with
    /// <see cref="BudgetReportCalculationException"/> - postings can no longer be added after
    /// reconciliation has already run, which would silently invalidate the finalized totals.
    /// </summary>
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

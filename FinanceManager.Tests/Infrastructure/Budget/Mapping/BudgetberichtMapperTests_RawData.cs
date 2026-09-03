using FinanceManager.Domain.Budget.ReportCalculation;
using FinanceManager.Infrastructure.Budget.Mapping;
using FinanceManager.Shared.Dtos.Budget;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using static FinanceManager.Tests.Budget.Domain.BudgetberichtTestFixtures;

namespace FinanceManager.Tests.Infrastructure.Budget.Mapping;

/// <summary>
/// Tests for <see cref="BudgetberichtMapper.MapToRawDataDto"/>: category/uncategorized grouping,
/// multi-month purpose merging, the missing-purpose-info logger warning path, and posting mapping
/// (valued, unvalued-matched, unbudgeted and cost-neutral).
/// </summary>
public sealed class BudgetberichtMapperTests_RawData
{
    private static readonly IReadOnlyDictionary<Guid, BudgetPurposeOverviewDto> EmptyPurposeInfo =
        new Dictionary<Guid, BudgetPurposeOverviewDto>();

    private static BudgetPurposeOverviewDto CreatePurposeInfo(BudgetPurposeDto purpose) => new(
        purpose.Id,
        purpose.OwnerUserId,
        purpose.Name,
        null,
        purpose.SourceType,
        purpose.SourceId,
        RuleCount: 1,
        BudgetSum: 0m,
        ActualSum: 0m,
        Variance: 0m,
        SourceName: "Source",
        SourceSymbolAttachmentId: null,
        BudgetCategoryId: purpose.BudgetCategoryId,
        BudgetCategoryName: null,
        ValuationType: purpose.ValuationType);

    /// <summary>
    /// Verifies that a purpose-level rule is aggregated onto the purpose's own row rather than its parent
    /// category's row, and that the matching posting is marked as valued for that purpose - a rule attached to a
    /// purpose must not be double-counted (or missed) at the category level.
    /// </summary>
    [Fact]
    public void MapToRawDataDto_CategorizedPurpose_AggregatesBudgetedAndActualAmounts()
    {
        var category = CreateCategory("Housing");
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Rent", BudgetSourceType.Contact, contactId, category.Id);
        var rule = CreatePurposeRule(purpose.Id, -500m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(new[] { category }, new[] { purpose }, new[] { rule });
        budgetbericht.AddPosting(CreateContactPosting(-500m, new DateTime(2026, 1, 5), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var purposeInfoById = new Dictionary<Guid, BudgetPurposeOverviewDto> { [purpose.Id] = CreatePurposeInfo(purpose) };

        var dto = BudgetberichtMapper.MapToRawDataDto(budgetbericht, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), purposeInfoById);

        // The category's own BudgetedExpense only reflects rules attached directly to the category; this
        // rule is purpose-level, so it is aggregated on the purpose row instead.
        var housing = dto.Categories.Should().ContainSingle(c => c.CategoryName == "Housing").Subject;
        housing.BudgetedExpense.Should().Be(0m);
        var rent = housing.Purposes.Should().ContainSingle(p => p.PurposeName == "Rent").Subject;
        rent.BudgetedExpense.Should().Be(-500m);
        rent.Postings.Should().ContainSingle(p => p.Amount == -500m && p.IsValuedForBudgetPurpose);
    }

    /// <summary>
    /// Verifies that when a report spans multiple months, all postings and budgeted amounts for the same purpose
    /// are merged into a single purpose row instead of one row per month - the raw-data report is a period summary,
    /// not a month-by-month breakdown.
    /// </summary>
    [Fact]
    public void MapToRawDataDto_MultiMonthPurpose_MergesIntoSinglePurposeRow()
    {
        var category = CreateCategory("Subscriptions");
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Streaming", BudgetSourceType.Contact, contactId, category.Id);
        var rule = CreatePurposeRule(purpose.Id, -10m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 2, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(new[] { category }, new[] { purpose }, new[] { rule });
        budgetbericht.AddPosting(CreateContactPosting(-10m, new DateTime(2026, 1, 5), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.AddPosting(CreateContactPosting(-10m, new DateTime(2026, 2, 5), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var purposeInfoById = new Dictionary<Guid, BudgetPurposeOverviewDto> { [purpose.Id] = CreatePurposeInfo(purpose) };

        var dto = BudgetberichtMapper.MapToRawDataDto(budgetbericht, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 28), purposeInfoById);

        var streaming = dto.Categories.Should().ContainSingle().Subject.Purposes.Should().ContainSingle().Subject;
        streaming.BudgetedExpense.Should().Be(-20m);
        streaming.Postings.Should().HaveCount(2);
        streaming.Postings.Should().OnlyContain(p => p.Amount == -10m);
    }

    /// <summary>
    /// Verifies that a purpose with no assigned category ends up in <c>UncategorizedPurposes</c> rather than being
    /// dropped or attached to a phantom category - purposes are not required to belong to a category, and this
    /// path must still surface their budgeted/actual amounts to the user.
    /// </summary>
    [Fact]
    public void MapToRawDataDto_PurposeWithoutCategory_AppearsInUncategorizedPurposes()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Gym", BudgetSourceType.Contact, contactId, categoryId: null);
        var rule = CreatePurposeRule(purpose.Id, -20m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });
        budgetbericht.AddPosting(CreateContactPosting(-20m, new DateTime(2026, 1, 5), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var purposeInfoById = new Dictionary<Guid, BudgetPurposeOverviewDto> { [purpose.Id] = CreatePurposeInfo(purpose) };

        var dto = BudgetberichtMapper.MapToRawDataDto(budgetbericht, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), purposeInfoById);

        dto.Categories.Should().BeEmpty();
        var gym = dto.UncategorizedPurposes.Should().ContainSingle(p => p.PurposeName == "Gym").Subject;
        gym.BudgetedExpense.Should().Be(-20m);
    }

    /// <summary>
    /// Verifies the defensive fallback when a purpose referenced by the report has no matching entry in the
    /// caller-supplied purpose-info lookup: the mapper still produces a row (with empty source name, an empty
    /// source id, and the exact-postings valuation default) instead of throwing, and logs a warning so the data
    /// inconsistency is observable - a purpose can outlive or precede its overview projection, so the mapper must
    /// degrade gracefully rather than fail the whole report.
    /// </summary>
    [Fact]
    public void MapToRawDataDto_MissingPurposeInfo_LogsWarning_AndFallsBackToDefaultSourceInfo()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Unknown Source", BudgetSourceType.Contact, contactId, categoryId: null);
        var rule = CreatePurposeRule(purpose.Id, -15m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });
        budgetbericht.Finish();

        var logger = new Mock<ILogger>();

        var dto = BudgetberichtMapper.MapToRawDataDto(budgetbericht, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), EmptyPurposeInfo, logger.Object);

        var unknown = dto.UncategorizedPurposes.Should().ContainSingle(p => p.PurposeName == "Unknown Source").Subject;
        unknown.BudgetSourceType.Should().Be(BudgetSourceType.Contact);
        unknown.SourceId.Should().Be(Guid.Empty);
        unknown.SourceName.Should().Be(string.Empty);
        unknown.ValuationType.Should().Be(BudgetValuationType.ExactPostings);

        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that for an <c>ExactPostings</c> purpose, a posting that matches the source (contact) but has the
    /// wrong sign relative to the rule is mapped as an <em>unvalued</em> match rather than being valued or dropped -
    /// the mismatch must remain visible on the purpose so the user can spot a miscategorized posting, while the
    /// budgeted amount itself still comes from the rule, not the mismatched posting.
    /// </summary>
    [Fact]
    public void MapToRawDataDto_SignMismatchedExactPosting_MapsAsUnvaluedMatch()
    {
        var contactId = Guid.NewGuid();
        var purpose = CreatePurpose("Insurance", BudgetSourceType.Contact, contactId, categoryId: null, valuationType: BudgetValuationType.ExactPostings);
        var rule = CreatePurposeRule(purpose.Id, -50m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), new[] { purpose }, new[] { rule });
        // Wrong sign for an ExactPostings expectation: matches the contact but not the expected direction.
        budgetbericht.AddPosting(CreateContactPosting(50m, new DateTime(2026, 1, 5), contactId), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var purposeInfoById = new Dictionary<Guid, BudgetPurposeOverviewDto> { [purpose.Id] = CreatePurposeInfo(purpose) };

        var dto = BudgetberichtMapper.MapToRawDataDto(budgetbericht, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), purposeInfoById);

        var insurance = dto.UncategorizedPurposes.Should().ContainSingle().Subject;
        insurance.Postings.Should().ContainSingle(p => p.Amount == 50m && !p.IsValuedForBudgetPurpose);
        insurance.BudgetedExpense.Should().Be(-50m);
    }

    /// <summary>
    /// Verifies that both plain unbudgeted postings and cost-neutral mirror postings (identified by a
    /// <c>GroupId</c>) end up in <c>UnbudgetedPostings</c>, each unvalued and unattributed to any category or
    /// purpose - the raw-data report's unbudgeted list is meant to catch everything that did not match a budget
    /// rule, regardless of whether it is a genuine unplanned transaction or a self-transfer.
    /// </summary>
    [Fact]
    public void MapToRawDataDto_UnbudgetedAndCostNeutralPostings_BothAppearInUnbudgetedPostings()
    {
        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(Array.Empty<BudgetCategoryDto>(), Array.Empty<BudgetPurposeDto>(), Array.Empty<BudgetRuleDto>());
        budgetbericht.AddPosting(CreateUnattributedPosting(-9.99m, new DateTime(2026, 1, 12)), BudgetReportDateBasis.BookingDate);
        var mirrorGroupId = Guid.NewGuid();
        budgetbericht.AddPosting(CreateUnattributedPosting(3m, new DateTime(2026, 1, 12), groupId: mirrorGroupId), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var dto = BudgetberichtMapper.MapToRawDataDto(budgetbericht, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), EmptyPurposeInfo);

        dto.UnbudgetedPostings.Should().HaveCount(2);
        dto.UnbudgetedPostings.Should().ContainSingle(p => p.Amount == -9.99m && p.GroupId == null);
        dto.UnbudgetedPostings.Should().ContainSingle(p => p.Amount == 3m && p.GroupId == mirrorGroupId);
        dto.UnbudgetedPostings.Should().OnlyContain(p => !p.IsValuedForBudgetPurpose && p.BudgetCategoryId == null && p.BudgetPurposeId == null);
    }

    /// <summary>
    /// Tests that a category-direct budget rule (BudgetCategoryId set, BudgetPurposeId null) correctly
    /// aggregates budgeted amounts on the category row's BudgetedExpense.
    /// </summary>
    [Fact]
    public void MapToRawDataDto_CategoryDirectRule_AggregatesBudgetedAmountsOnCategoryRow()
    {
        var category = CreateCategory("Utilities");
        var rule = CreateCategoryRule(category.Id, -150m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(new[] { category }, Array.Empty<BudgetPurposeDto>(), new[] { rule });
        // Add an actual posting that matches the category-level rule (no purpose involved).
        budgetbericht.AddPosting(CreateUnattributedPosting(-150m, new DateTime(2026, 1, 15)), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var dto = BudgetberichtMapper.MapToRawDataDto(budgetbericht, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), EmptyPurposeInfo);

        // The category-direct rule's amount (-150) should appear on the category row's BudgetedExpense.
        var utilities = dto.Categories.Should().ContainSingle(c => c.CategoryName == "Utilities").Subject;
        utilities.BudgetedExpense.Should().Be(-150m);
        utilities.BudgetedIncome.Should().Be(0m);
        utilities.BudgetedTarget.Should().Be(-150m);
        utilities.Purposes.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that a category-direct budget rule with positive amount (income) correctly
    /// aggregates budgeted amounts on the category row's BudgetedIncome.
    /// </summary>
    [Fact]
    public void MapToRawDataDto_CategoryDirectRuleIncome_AggregatesIncomeOnCategoryRow()
    {
        var category = CreateCategory("Bonus");
        var rule = CreateCategoryRule(category.Id, 500m, BudgetIntervalType.Monthly, new DateOnly(2026, 1, 1));

        var budgetbericht = new Budgetbericht(new DateOnly(2026, 1, 1), 1, BudgetReportInterval.Month, BudgetReportDateBasis.BookingDate);
        budgetbericht.SetPlanung(new[] { category }, Array.Empty<BudgetPurposeDto>(), new[] { rule });
        budgetbericht.AddPosting(CreateUnattributedPosting(500m, new DateTime(2026, 1, 15)), BudgetReportDateBasis.BookingDate);
        budgetbericht.Finish();

        var dto = BudgetberichtMapper.MapToRawDataDto(budgetbericht, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), EmptyPurposeInfo);

        // The category-direct rule's income (500) should appear on the category row's BudgetedIncome.
        var bonus = dto.Categories.Should().ContainSingle(c => c.CategoryName == "Bonus").Subject;
        bonus.BudgetedIncome.Should().Be(500m);
        bonus.BudgetedExpense.Should().Be(0m);
        bonus.BudgetedTarget.Should().Be(500m);
        bonus.Purposes.Should().BeEmpty();
    }
}

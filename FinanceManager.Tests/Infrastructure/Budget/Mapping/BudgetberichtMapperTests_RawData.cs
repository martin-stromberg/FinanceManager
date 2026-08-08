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
}

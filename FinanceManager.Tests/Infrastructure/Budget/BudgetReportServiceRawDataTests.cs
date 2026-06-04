using FinanceManager.Application.Budget;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Postings;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Infrastructure.Budget;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Shared.Dtos.Securities;
using FluentAssertions;
using Moq;

namespace FinanceManager.Tests.Infrastructure.Budget;

/// <summary>
/// Verifies purpose-pattern handling in budget report raw data generation.
/// </summary>
public sealed class BudgetReportServiceRawDataTests
{
    /// <summary>
    /// Ensures case-insensitive contains matching allocates postings to the matching purpose.
    /// </summary>
    [Fact]
    public async Task GetRawDataAsync_ShouldMatchPurposePattern_WhenContainsIsCaseInsensitive()
    {
        var ownerUserId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var purposeId = Guid.NewGuid();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 31);
        var postingId = Guid.NewGuid();

        var purpose = new BudgetPurposeOverviewDto(
            purposeId,
            ownerUserId,
            "Utilities",
            null,
            BudgetSourceType.Contact,
            contactId,
            1,
            -60m,
            0m,
            0m,
            "Utility Provider",
            null,
            null,
            null);

        var rule = new BudgetRuleDto(
            Guid.NewGuid(),
            ownerUserId,
            purposeId,
            null,
            -60m,
            BudgetIntervalType.Monthly,
            null,
            from,
            null,
            "st6464646464",
            false);

        var posting = CreateContactPosting(
            postingId,
            contactId,
            new DateTime(2026, 1, 20),
            -60m,
            "Abrechnung ST6464646464 Januar");

        var sut = CreateSut(
            ownerUserId,
            from,
            to,
            new[] { purpose },
            new[] { rule },
            new[] { posting },
            new ContactDto(contactId, "Utility Provider", ContactType.Organization, null, null, false, null));

        var result = await sut.GetRawDataAsync(ownerUserId, from, to, BudgetReportDateBasis.BookingDate, CancellationToken.None);

        result.UncategorizedPurposes.Should().ContainSingle(x => x.PurposeId == purposeId);
        result.UncategorizedPurposes.Single(x => x.PurposeId == purposeId).Postings.Should().ContainSingle(x => x.PostingId == postingId);
        result.UnbudgetedPostings.Should().BeEmpty();
    }

    /// <summary>
    /// Ensures regex matching allocates only matching postings and keeps non-matching postings unbudgeted.
    /// </summary>
    [Fact]
    public async Task GetRawDataAsync_ShouldSplitMatchingAndUnbudgetedPostings_WhenRegexPatternIsUsed()
    {
        var ownerUserId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var purposeId = Guid.NewGuid();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 31);
        var matchingPostingId = Guid.NewGuid();
        var nonMatchingPostingId = Guid.NewGuid();

        var purpose = new BudgetPurposeOverviewDto(
            purposeId,
            ownerUserId,
            "Utilities",
            null,
            BudgetSourceType.Contact,
            contactId,
            1,
            -60m,
            0m,
            0m,
            "Utility Provider",
            null,
            null,
            null);

        var rule = new BudgetRuleDto(
            Guid.NewGuid(),
            ownerUserId,
            purposeId,
            null,
            -60m,
            BudgetIntervalType.Monthly,
            null,
            from,
            null,
            "ST\\d{10}",
            true);

        var postings = new[]
        {
            CreateContactPosting(
                matchingPostingId,
                contactId,
                new DateTime(2026, 1, 20),
                -60m,
                "Abrechnung ST6464646464 Januar"),
            CreateContactPosting(
                nonMatchingPostingId,
                contactId,
                new DateTime(2026, 1, 21),
                -40m,
                "Service ohne Vertragsnummer")
        };

        var sut = CreateSut(
            ownerUserId,
            from,
            to,
            new[] { purpose },
            new[] { rule },
            postings,
            new ContactDto(contactId, "Utility Provider", ContactType.Organization, null, null, false, null));

        var result = await sut.GetRawDataAsync(ownerUserId, from, to, BudgetReportDateBasis.BookingDate, CancellationToken.None);

        var purposePostings = result.UncategorizedPurposes.Single(x => x.PurposeId == purposeId).Postings;
        purposePostings.Should().ContainSingle(x => x.PostingId == matchingPostingId);
        result.UnbudgetedPostings.Should().ContainSingle(x => x.PostingId == nonMatchingPostingId);
    }

    /// <summary>
    /// Ensures empty posting descriptions do not match non-empty patterns.
    /// </summary>
    [Fact]
    public async Task GetRawDataAsync_ShouldTreatEmptyPurposeTextAsNonMatch_WhenPatternExists()
    {
        var ownerUserId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var purposeId = Guid.NewGuid();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 31);
        var postingId = Guid.NewGuid();

        var purpose = new BudgetPurposeOverviewDto(
            purposeId,
            ownerUserId,
            "Utilities",
            null,
            BudgetSourceType.Contact,
            contactId,
            1,
            -60m,
            0m,
            0m,
            "Utility Provider",
            null,
            null,
            null);

        var rule = new BudgetRuleDto(
            Guid.NewGuid(),
            ownerUserId,
            purposeId,
            null,
            -60m,
            BudgetIntervalType.Monthly,
            null,
            from,
            null,
            "ABC123",
            false);

        var posting = CreateContactPosting(
            postingId,
            contactId,
            new DateTime(2026, 1, 20),
            -60m,
            null);

        var sut = CreateSut(
            ownerUserId,
            from,
            to,
            new[] { purpose },
            new[] { rule },
            new[] { posting },
            new ContactDto(contactId, "Utility Provider", ContactType.Organization, null, null, false, null));

        var result = await sut.GetRawDataAsync(ownerUserId, from, to, BudgetReportDateBasis.BookingDate, CancellationToken.None);

        result.UncategorizedPurposes.Single(x => x.PurposeId == purposeId).Postings.Should().BeEmpty();
        result.UnbudgetedPostings.Should().ContainSingle(x => x.PostingId == postingId);
    }

    /// <summary>
    /// Ensures problematic regex patterns do not throw and unmatched postings stay unbudgeted.
    /// </summary>
    [Fact]
    public async Task GetRawDataAsync_ShouldKeepPostingUnbudgeted_WhenRegexMatchTimesOutOrFails()
    {
        var ownerUserId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var purposeId = Guid.NewGuid();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 31);
        var postingId = Guid.NewGuid();

        var purpose = new BudgetPurposeOverviewDto(
            purposeId,
            ownerUserId,
            "Utilities",
            null,
            BudgetSourceType.Contact,
            contactId,
            1,
            -60m,
            0m,
            0m,
            "Utility Provider",
            null,
            null,
            null);

        var rule = new BudgetRuleDto(
            Guid.NewGuid(),
            ownerUserId,
            purposeId,
            null,
            -60m,
            BudgetIntervalType.Monthly,
            null,
            from,
            null,
            "^(a+)+$",
            true);

        var longPurposeText = $"{new string('a', 20000)}!";
        var posting = CreateContactPosting(
            postingId,
            contactId,
            new DateTime(2026, 1, 20),
            -60m,
            longPurposeText);

        var sut = CreateSut(
            ownerUserId,
            from,
            to,
            new[] { purpose },
            new[] { rule },
            new[] { posting },
            new ContactDto(contactId, "Utility Provider", ContactType.Organization, null, null, false, null));

        var act = async () => await sut.GetRawDataAsync(ownerUserId, from, to, BudgetReportDateBasis.BookingDate, CancellationToken.None);
        var result = await act.Should().NotThrowAsync();

        result.Subject.UnbudgetedPostings.Should().ContainSingle(x => x.PostingId == postingId);
    }

    private static BudgetReportService CreateSut(
        Guid ownerUserId,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<BudgetPurposeOverviewDto> purposes,
        IReadOnlyList<BudgetRuleDto> rules,
        IReadOnlyList<PostingServiceDto> postings,
        ContactDto contact)
    {
        var purposeService = new Mock<IBudgetPurposeService>();
        purposeService
            .Setup(x => x.ListOverviewAsync(ownerUserId, 0, 5000, null, null, from, to, null, It.IsAny<CancellationToken>(), BudgetReportDateBasis.BookingDate))
            .ReturnsAsync(purposes);

        var categoryService = new Mock<IBudgetCategoryService>();
        categoryService
            .Setup(x => x.ListOverviewAsync(ownerUserId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetCategoryOverviewDto>());

        var ruleService = new Mock<IBudgetRuleService>();
        ruleService
            .Setup(x => x.ListByPurposeAsync(ownerUserId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid __, CancellationToken ___) => rules);
        ruleService
            .Setup(x => x.ListByCategoryAsync(ownerUserId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetRuleDto>());

        var postingsService = new Mock<IPostingsQueryService>();
        postingsService
            .Setup(x => x.GetContactPostingsAsync(contact.Id, 0, 5000, null, from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MaxValue), ownerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(postings);

        var contactService = new Mock<IContactService>();
        contactService
            .Setup(x => x.ListAsync(ownerUserId, 0, 5000, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { contact });

        var savingsPlanService = new Mock<ISavingsPlanService>();
        savingsPlanService
            .Setup(x => x.ListAsync(ownerUserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SavingsPlanDto>());

        var securityService = new Mock<ISecurityService>();
        securityService
            .Setup(x => x.ListAsync(ownerUserId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SecurityDto>());

        var cacheService = new Mock<IReportCacheService>();
        cacheService
            .Setup(x => x.GetBudgetReportRawDataAsync(ownerUserId, from, to, BudgetReportDateBasis.BookingDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BudgetReportRawDataDto?)null);
        cacheService
            .Setup(x => x.SetBudgetReportRawDataAsync(ownerUserId, from, to, BudgetReportDateBasis.BookingDate, It.IsAny<BudgetReportRawDataDto>(), false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new BudgetReportService(
            purposeService.Object,
            categoryService.Object,
            ruleService.Object,
            postingsService.Object,
            contactService.Object,
            savingsPlanService.Object,
            securityService.Object,
            cacheService.Object);
    }

    private static PostingServiceDto CreateContactPosting(Guid postingId, Guid contactId, DateTime bookingDate, decimal amount, string? subject)
    {
        return new PostingServiceDto(
            postingId,
            bookingDate,
            bookingDate,
            amount,
            PostingKind.Contact,
            null,
            contactId,
            null,
            null,
            Guid.NewGuid(),
            subject,
            "Utility Provider",
            null,
            null,
            null,
            Guid.Empty,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }
}
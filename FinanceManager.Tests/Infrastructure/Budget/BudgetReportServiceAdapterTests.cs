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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FinanceManager.Tests.Infrastructure.Budget;

/// <summary>
/// Adapter-level tests for <see cref="BudgetReportService"/>: verifies that <c>GetRawDataAsync</c> and
/// <c>GetMonthlyKpiAsync</c> correctly drive the <c>Budgetbericht</c> domain model from the underlying
/// application services and integrate with <see cref="IReportCacheService"/>.
/// </summary>
public sealed class BudgetReportServiceAdapterTests
{
    private readonly Mock<IBudgetPurposeService> _purposes = new();
    private readonly Mock<IBudgetCategoryService> _categories = new();
    private readonly Mock<IBudgetRuleService> _rules = new();
    private readonly Mock<IPostingsQueryService> _postings = new();
    private readonly Mock<IContactService> _contacts = new();
    private readonly Mock<ISavingsPlanService> _savingsPlans = new();
    private readonly Mock<ISecurityService> _securities = new();
    private readonly Mock<IReportCacheService> _cacheService = new();

    /// <summary>
    /// Wires up the default, "happy path" stubs shared by every test in this class (empty category/purpose/
    /// contact/savings-plan/security lists and a cache miss), so individual tests only need to override the
    /// specific dependency they are exercising instead of re-stubbing every collaborator from scratch.
    /// </summary>
    public BudgetReportServiceAdapterTests()
    {
        _categories.Setup(x => x.ListAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetCategoryDto>());
        _purposes.Setup(x => x.ListAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetPurposeDto>());
        _purposes.Setup(x => x.ListOverviewAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), null, null,
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), null, It.IsAny<CancellationToken>(), It.IsAny<BudgetReportDateBasis>()))
            .ReturnsAsync(Array.Empty<BudgetPurposeOverviewDto>());
        _contacts.Setup(x => x.ListAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ContactDto>());
        _savingsPlans.Setup(x => x.ListAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SavingsPlanDto>());
        _securities.Setup(x => x.ListAsync(It.IsAny<Guid>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SecurityDto>());
        _cacheService.Setup(x => x.GetBudgetReportRawDataAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<BudgetReportDateBasis>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BudgetReportRawDataDto?)null);
    }

    /// <summary>
    /// Constructs a <see cref="BudgetReportService"/> wired to this fixture's mocked collaborators, so each
    /// test can adjust only the mocks relevant to its scenario before creating the service under test.
    /// </summary>
    /// <returns>A <see cref="BudgetReportService"/> ready to exercise in a test.</returns>
    private BudgetReportService CreateService() => new(
        _purposes.Object,
        _categories.Object,
        _rules.Object,
        _postings.Object,
        _contacts.Object,
        _savingsPlans.Object,
        _securities.Object,
        _cacheService.Object,
        NullLogger<BudgetReportService>.Instance);

    /// <summary>
    /// Verifies that on a cache hit, <c>GetRawDataAsync</c> returns the cached <see cref="BudgetReportRawDataDto"/>
    /// as-is and never touches the category service to rebuild the <c>Budgetbericht</c> from scratch. This is
    /// the core performance guarantee of the report cache: building the raw data involves several downstream
    /// service calls (categories, purposes, rules, contacts, postings), so a hit must short-circuit all of them.
    /// </summary>
    [Fact]
    public async Task GetRawDataAsync_ReturnsCachedResult_WithoutRebuildingBudgetbericht_WhenCacheHit()
    {
        var cached = new BudgetReportRawDataDto
        {
            PeriodStart = new DateTime(2026, 1, 1),
            PeriodEnd = new DateTime(2026, 1, 31)
        };
        _cacheService.Setup(x => x.GetBudgetReportRawDataAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<BudgetReportDateBasis>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var service = CreateService();
        var result = await service.GetRawDataAsync(Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), BudgetReportDateBasis.BookingDate, CancellationToken.None);

        result.Should().BeSameAs(cached);
        _categories.Verify(x => x.ListAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that passing <c>ignoreCache: true</c> makes <c>GetRawDataAsync</c> skip the cache lookup
    /// entirely, even though a cached entry exists and would otherwise be returned. This is the escape hatch
    /// callers need to force a fresh rebuild (e.g. an explicit "refresh report" action) without depending on
    /// the cache having already been invalidated by other means.
    /// </summary>
    [Fact]
    public async Task GetRawDataAsync_IgnoresCache_WhenIgnoreCacheIsTrue()
    {
        var cached = new BudgetReportRawDataDto
        {
            PeriodStart = new DateTime(2026, 1, 1),
            PeriodEnd = new DateTime(2026, 1, 31)
        };
        _cacheService.Setup(x => x.GetBudgetReportRawDataAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<BudgetReportDateBasis>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var service = CreateService();
        await service.GetRawDataAsync(Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), BudgetReportDateBasis.BookingDate, CancellationToken.None, ignoreCache: true);

        _cacheService.Verify(x => x.GetBudgetReportRawDataAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<BudgetReportDateBasis>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// End-to-end integration check for the cache-miss path: verifies that <c>GetRawDataAsync</c> correctly
    /// assembles a <c>Budgetbericht</c> by pulling categories, purposes, rules, contacts and matching postings
    /// from their respective services, that a purpose-level rule's budgeted amount is attributed to the purpose
    /// row rather than its parent category (the category's own <c>BudgetedExpense</c> stays 0), and that the
    /// freshly built result is written back to the cache via <c>SetBudgetReportRawDataAsync</c> so subsequent
    /// requests can hit the cache instead of repeating this assembly.
    /// </summary>
    [Fact]
    public async Task GetRawDataAsync_BuildsBudgetberichtFromServices_AndStoresResultInCache_OnCacheMiss()
    {
        var ownerUserId = Guid.NewGuid();
        var category = new BudgetCategoryDto(Guid.NewGuid(), ownerUserId, "Housing");
        var contactId = Guid.NewGuid();
        var purpose = new BudgetPurposeDto(Guid.NewGuid(), ownerUserId, "Rent", null, BudgetSourceType.Contact, contactId, category.Id);
        var rule = new BudgetRuleDto(Guid.NewGuid(), ownerUserId, purpose.Id, null, -500m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 1, 1), null);

        _categories.Setup(x => x.ListAsync(ownerUserId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { category });
        _purposes.Setup(x => x.ListAsync(ownerUserId, It.IsAny<int>(), It.IsAny<int>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { purpose });
        _rules.Setup(x => x.ListByPurposeAsync(ownerUserId, purpose.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { rule });
        _rules.Setup(x => x.ListByCategoryAsync(ownerUserId, category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<BudgetRuleDto>());

        var contact = new ContactDto(contactId, "Landlord", ContactType.Organization, null, null, false, null);
        _contacts.Setup(x => x.ListAsync(ownerUserId, It.IsAny<int>(), It.IsAny<int>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { contact });

        var posting = new PostingServiceDto(
            Guid.NewGuid(), new DateTime(2026, 1, 5), new DateTime(2026, 1, 5), -500m, PostingKind.Contact,
            Guid.NewGuid(), contactId, null, null, Guid.NewGuid(), "Rent January", "Landlord", "Lastschrift",
            null, null, Guid.Empty, null, null, null, null, null, null, null, null, false, false, null, null);
        _postings.Setup(x => x.GetContactPostingsAsync(contactId, It.IsAny<int>(), It.IsAny<int>(), null, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), ownerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { posting });

        var service = CreateService();
        var result = await service.GetRawDataAsync(ownerUserId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), BudgetReportDateBasis.BookingDate, CancellationToken.None);

        // The category's own Budgeted* fields reflect only rules attached directly to the category;
        // this scenario has a purpose-level rule, so the category's own amount stays 0 while the
        // purpose row underneath it carries the -500 budget and the matched posting.
        var housing = result.Categories.Should().ContainSingle(c => c.CategoryName == "Housing").Subject;
        housing.BudgetedExpense.Should().Be(0m);
        var rentPurpose = housing.Purposes.Should().ContainSingle(p => p.PurposeName == "Rent").Subject;
        rentPurpose.BudgetedExpense.Should().Be(-500m);
        rentPurpose.Postings.Should().ContainSingle(p => p.Amount == -500m && p.IsValuedForBudgetPurpose);
        _cacheService.Verify(x => x.SetBudgetReportRawDataAsync(ownerUserId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), BudgetReportDateBasis.BookingDate, result, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that <c>GetMonthlyKpiAsync</c> correctly produces the planned/actual income and actual result
    /// figures for a single month, driven by a monthly income rule tied to a purpose and one matching posting.
    /// This exercises the full adapter path (service aggregation through to KPI mapping), complementing the
    /// mapper-level KPI tests which operate on an already-built <c>Budgetbericht</c> directly.
    /// </summary>
    [Fact]
    public async Task GetMonthlyKpiAsync_ComputesKpiForSingleMonth_FromMatchingPosting()
    {
        var ownerUserId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var purpose = new BudgetPurposeDto(Guid.NewGuid(), ownerUserId, "Salary", null, BudgetSourceType.Contact, contactId, null);
        var rule = new BudgetRuleDto(Guid.NewGuid(), ownerUserId, purpose.Id, null, 3000m, BudgetIntervalType.Monthly, null, new DateOnly(2026, 2, 1), null);

        _purposes.Setup(x => x.ListAsync(ownerUserId, It.IsAny<int>(), It.IsAny<int>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { purpose });
        _rules.Setup(x => x.ListByPurposeAsync(ownerUserId, purpose.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { rule });
        _rules.Setup(x => x.ListByCategoryAsync(ownerUserId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<BudgetRuleDto>());

        var contact = new ContactDto(contactId, "Employer", ContactType.Organization, null, null, false, null);
        _contacts.Setup(x => x.ListAsync(ownerUserId, It.IsAny<int>(), It.IsAny<int>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { contact });

        var posting = new PostingServiceDto(
            Guid.NewGuid(), new DateTime(2026, 2, 25), new DateTime(2026, 2, 25), 3000m, PostingKind.Contact,
            Guid.NewGuid(), contactId, null, null, Guid.NewGuid(), "Gehalt Februar", "Employer", "Gutschrift",
            null, null, Guid.Empty, null, null, null, null, null, null, null, null, false, false, null, null);
        _postings.Setup(x => x.GetContactPostingsAsync(contactId, It.IsAny<int>(), It.IsAny<int>(), null, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), ownerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { posting });

        var service = CreateService();
        var kpi = await service.GetMonthlyKpiAsync(ownerUserId, new DateOnly(2026, 2, 1), BudgetReportDateBasis.BookingDate, CancellationToken.None);

        kpi.PlannedIncome.Should().Be(3000m);
        kpi.ActualIncome.Should().Be(3000m);
        kpi.ActualResult.Should().Be(3000m);
    }
}

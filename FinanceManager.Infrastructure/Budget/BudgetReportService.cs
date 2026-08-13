using FinanceManager.Application.Budget;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Postings;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Domain.Budget.ReportCalculation;
using FinanceManager.Infrastructure.Budget.Mapping;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.Postings;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Budget;

/// <summary>
/// Adapter between <see cref="IBudgetReportService"/> and the <see cref="Budgetbericht"/> domain model:
/// loads categories/purposes/rules and postings from the existing application services, drives the
/// <see cref="Budgetbericht"/> lifecycle (SetPlanung, AddPosting, Finish) and maps the result to the
/// API-facing DTOs via <see cref="BudgetberichtMapper"/>.
/// </summary>
public sealed class BudgetReportService : IBudgetReportService
{
    // Upper bound used for every paged listing call (purposes, contacts, savings plans, postings) when
    // building a Budgetbericht: large enough to effectively load "all" of a user's data in one page for
    // the report calculation, which always needs the complete data set for its period rather than a
    // partial page.
    private const int MaxPageSize = 5000;

    private readonly IBudgetPurposeService _purposes;
    private readonly IBudgetCategoryService _categories;
    private readonly IBudgetRuleService _rules;
    private readonly IPostingsQueryService _postings;
    private readonly IContactService _contacts;
    private readonly ISavingsPlanService _savingsPlans;
    private readonly ISecurityService _securities;
    private readonly IReportCacheService _cacheService;
    private readonly ILogger<BudgetReportService> _logger;

    /// <summary>
    /// Creates a new <see cref="BudgetReportService"/>.
    /// </summary>
    /// <param name="purposes">Service providing budget purposes.</param>
    /// <param name="categories">Service providing budget categories.</param>
    /// <param name="rules">Service providing budget rules.</param>
    /// <param name="postings">Service for retrieving individual postings.</param>
    /// <param name="contacts">Service providing contacts for the owner.</param>
    /// <param name="savingsPlans">Service providing savings plans for the owner.</param>
    /// <param name="securities">Service providing securities for the owner.</param>
    /// <param name="cacheService">Service used to cache raw report data.</param>
    /// <param name="logger">Logger used to record data inconsistencies encountered while mapping the report.</param>
    public BudgetReportService(
        IBudgetPurposeService purposes,
        IBudgetCategoryService categories,
        IBudgetRuleService rules,
        IPostingsQueryService postings,
        IContactService contacts,
        ISavingsPlanService savingsPlans,
        ISecurityService securities,
        IReportCacheService cacheService,
        ILogger<BudgetReportService> logger)
    {
        _purposes = purposes;
        _categories = categories;
        _rules = rules;
        _postings = postings;
        _contacts = contacts;
        _savingsPlans = savingsPlans;
        _securities = securities;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BudgetReportRawDataDto> GetRawDataAsync(
        Guid ownerUserId,
        DateOnly from,
        DateOnly to,
        BudgetReportDateBasis dateBasis,
        CancellationToken ct,
        bool ignoreCache = false)
    {
        if (!ignoreCache)
        {
            var cached = await _cacheService.GetBudgetReportRawDataAsync(ownerUserId, from, to, dateBasis, ct);
            if (cached != null)
            {
                return cached;
            }
        }

        var (budgetbericht, purposeInfoById) = await BuildBudgetberichtAsync(ownerUserId, from, to, dateBasis, ct);
        var result = BudgetberichtMapper.MapToRawDataDto(budgetbericht, from, to, purposeInfoById, _logger);

        await _cacheService.SetBudgetReportRawDataAsync(ownerUserId, from, to, dateBasis, result, needsRefresh: false, ct);

        return result;
    }

    /// <inheritdoc />
    public async Task<MonthlyBudgetKpiDto> GetMonthlyKpiAsync(Guid userId, DateOnly? date, BudgetReportDateBasis dateBasis, CancellationToken ct)
    {
        var from = new DateOnly(date?.Year ?? DateTime.Now.Year, date?.Month ?? DateTime.Now.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var (budgetbericht, _) = await BuildBudgetberichtAsync(userId, from, to, dateBasis, ct);
        var entries = budgetbericht.GetCurrentResult();

        return BudgetberichtMapper.MapToMonthlyKpiDto(entries);
    }

    /// <inheritdoc />
    public async Task<BudgetReportDto> GetReportAsync(
        Guid ownerUserId,
        DateOnly asOfDate,
        int months,
        BudgetReportInterval interval,
        BudgetReportValueScope categoryValueScope,
        BudgetReportDateBasis dateBasis,
        CancellationToken ct)
    {
        var to = new DateOnly(asOfDate.Year, asOfDate.Month, DateTime.DaysInMonth(asOfDate.Year, asOfDate.Month));
        var from = new DateOnly(to.Year, to.Month, 1).AddMonths(-(months - 1));

        var (budgetbericht, purposeInfoById) = await BuildBudgetberichtAsync(ownerUserId, from, to, dateBasis, ct);

        var periods = BudgetberichtMapper.MapToPeriodDtos(budgetbericht.GetCumulativeResult());

        // "LastInterval" restricts the category/purpose table to the report range's last (most recent)
        // month - the period table above is always built at monthly granularity, so that last period is
        // always exactly the "to" month.
        var entries = categoryValueScope == BudgetReportValueScope.LastInterval
            ? budgetbericht.GetCurrentResult(new DateOnly(to.Year, to.Month, 1))
            : budgetbericht.GetCurrentResult();

        var categories = BudgetberichtMapper.MapToReportCategoryDtos(entries, purposeInfoById);

        return new BudgetReportDto(from, to, interval, periods, categories);
    }

    private async Task<(Budgetbericht Budgetbericht, Dictionary<Guid, BudgetPurposeOverviewDto> PurposeInfoById)> BuildBudgetberichtAsync(
        Guid ownerUserId,
        DateOnly from,
        DateOnly to,
        BudgetReportDateBasis dateBasis,
        CancellationToken ct)
    {
        var categories = await _categories.ListAsync(ownerUserId, ct);
        var purposes = await _purposes.ListAsync(ownerUserId, 0, MaxPageSize, null, null, ct);
        var purposeOverviews = await _purposes.ListOverviewAsync(ownerUserId, 0, MaxPageSize, null, null, from, to, null, ct, dateBasis);
        var purposeInfoById = purposeOverviews.ToDictionary(p => p.Id);

        var rules = new List<BudgetRuleDto>();
        foreach (var purpose in purposes)
        {
            rules.AddRange(await _rules.ListByPurposeAsync(ownerUserId, purpose.Id, ct));
        }

        foreach (var category in categories)
        {
            rules.AddRange(await _rules.ListByCategoryAsync(ownerUserId, category.Id, ct));
        }

        var monthCount = ((to.Year - from.Year) * 12) + (to.Month - from.Month) + 1;
        var budgetbericht = new Budgetbericht(from, monthCount, BudgetReportInterval.Month, dateBasis);
        budgetbericht.SetPlanung(categories, purposes, rules);

        var realizations = await BuildRealizationsAsync(ownerUserId, from, to, dateBasis, ct);
        foreach (var realization in realizations)
        {
            budgetbericht.AddPosting(realization, dateBasis);
        }

        budgetbericht.Finish();

        return (budgetbericht, purposeInfoById);
    }

    private async Task<List<MonthlyBudgetRealization>> BuildRealizationsAsync(
        Guid ownerUserId,
        DateOnly from,
        DateOnly to,
        BudgetReportDateBasis dateBasis,
        CancellationToken ct)
    {
        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(TimeOnly.MaxValue);

        var contacts = await _contacts.ListAsync(ownerUserId, 0, MaxPageSize, null, null, ct);
        var savingsPlans = await _savingsPlans.ListAsync(ownerUserId, onlyActive: true, ct);
        var securities = await _securities.ListAsync(ownerUserId, false, ct);

        var contactPostings = new List<PostingServiceDto>();
        foreach (var contact in contacts)
        {
            var postings = await _postings.GetContactPostingsAsync(contact.Id, 0, MaxPageSize, null, fromDt, toDt, ownerUserId, ct);
            contactPostings.AddRange(postings);
        }

        contactPostings = contactPostings.GroupBy(p => p.Id).Select(g => g.First()).ToList();

        var savingsPlanPostings = new List<PostingServiceDto>();
        foreach (var plan in savingsPlans)
        {
            var postings = await _postings.GetSavingsPlanPostingsAsync(plan.Id, 0, MaxPageSize, null, fromDt, toDt, ownerUserId, ct);
            savingsPlanPostings.AddRange(postings);
        }

        var securityPostings = new List<PostingServiceDto>();
        foreach (var security in securities)
        {
            var postings = await _postings.GetSecurityPostingsAsync(security.Id, 0, MaxPageSize, fromDt, toDt, ownerUserId, ct);
            securityPostings.AddRange(postings);
        }

        var savingsPlanByGroup = savingsPlanPostings
            .Where(p => p.GroupId != Guid.Empty)
            .GroupBy(p => p.GroupId)
            .ToDictionary(g => g.Key, g => g.First());

        var securityByGroup = securityPostings
            .Where(p => p.GroupId != Guid.Empty)
            .GroupBy(p => p.GroupId)
            .ToDictionary(g => g.Key, g => g.First());

        var selfContactId = contacts.FirstOrDefault(c => c.Type == ContactType.Self)?.Id;

        var realizations = contactPostings.Select(p =>
        {
            var contact = contacts.FirstOrDefault(c => c.Id == p.ContactId);

            Guid? savingsPlanId = p.SavingsPlanId;
            if (savingsPlanId == null && p.GroupId != Guid.Empty && savingsPlanByGroup.TryGetValue(p.GroupId, out var spPosting))
            {
                savingsPlanId = spPosting.SavingsPlanId;
            }

            Guid? securityId = p.SecurityId;
            if (securityId == null && p.GroupId != Guid.Empty && securityByGroup.TryGetValue(p.GroupId, out var secPosting))
            {
                securityId = secPosting.SecurityId;
            }

            return new MonthlyBudgetRealization
            {
                PostingId = p.Id,
                BookingDate = p.BookingDate,
                ValutaDate = p.ValutaDate,
                ContactId = p.ContactId,
                ContactGroupId = contact?.CategoryId,
                SavingsPlanId = savingsPlanId,
                Amount = p.Amount,
                Purpose = p.Subject,
                Description = p.Description,
                GroupId = p.GroupId != Guid.Empty ? p.GroupId : null,
                IsSelfContact = selfContactId.HasValue && p.ContactId == selfContactId.Value,
                PostingKind = p.Kind,
                AccountId = p.AccountId,
                AccountName = p.LinkedPostingAccountName ?? p.BankPostingAccountName,
                ContactName = contact?.Name ?? p.RecipientName,
                SavingsPlanName = savingsPlanId.HasValue ? savingsPlans.FirstOrDefault(sp => sp.Id == savingsPlanId.Value)?.Name : null,
                SecurityId = securityId,
                SecurityName = securityId.HasValue ? securities.FirstOrDefault(sec => sec.Id == securityId.Value)?.Name : null
            };
        });

        return realizations.Where(r => IsWithinRequestedRange(r, from, to, dateBasis)).ToList();
    }

    private static bool IsWithinRequestedRange(MonthlyBudgetRealization posting, DateOnly from, DateOnly to, BudgetReportDateBasis dateBasis)
    {
        if (dateBasis == BudgetReportDateBasis.ValutaDate)
        {
            if (!posting.ValutaDate.HasValue)
            {
                return false;
            }

            var valutaDate = DateOnly.FromDateTime(posting.ValutaDate.Value);
            return valutaDate >= from && valutaDate <= to;
        }

        var bookingDate = DateOnly.FromDateTime(posting.BookingDate);
        return bookingDate >= from && bookingDate <= to;
    }
}

using FinanceManager.Application;
using FinanceManager.Application.Budget;
using FinanceManager.Domain.Budget;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Web.Infrastructure.ApiErrors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Net.Mime;
using FinanceManager.Infrastructure;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Domain.Postings;
using FinanceManager.Application.Budget;
using FinanceManager.Web.Infrastructure;

namespace FinanceManager.Web.Controllers;

/// <summary>
/// Provides budget reporting endpoints.
/// </summary>
[ApiController]
[Route("api/budget/report")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class BudgetReportsController : ControllerBase
{
    private const string Origin = "API_BudgetReport";

    private readonly IBudgetReportService _reports;
    private readonly ICurrentUserService _current;
    private readonly ILogger<BudgetReportsController> _logger;
    private readonly IStringLocalizer<Controller> _localizer;
    private readonly AppDbContext _db;
    private readonly IBudgetPurposeService _purposes;
    private readonly IBudgetReportExportService _export;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public BudgetReportsController(
        IBudgetReportService reports,
        IBudgetPurposeService purposes,
        AppDbContext db,
        ICurrentUserService current,
        ILogger<BudgetReportsController> logger,
        IStringLocalizer<Controller> localizer,
        IBudgetReportExportService export)
    {
        _reports = reports;
        _purposes = purposes;
        _db = db;
        _current = current;
        _logger = logger;
        _localizer = localizer;
        _export = export;
    }

    /// <summary>
    /// Exports all postings of the budget report for the total report range as an Excel file.
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportAsync([FromQuery] DateOnly asOf, [FromQuery] int months = 12, [FromQuery] BudgetReportDateBasis dateBasis = BudgetReportDateBasis.BookingDate, CancellationToken ct = default)
    {
        try
        {
            var req = new BudgetReportExportRequest(asOf, months, dateBasis);
            var (contentType, fileName, content) = await _export.GenerateXlsxAsync(_current.UserId, req, ct);
            return new StreamCallbackResult(contentType, async (output, token) =>
            {
                await using (content)
                {
                    await content.CopyToAsync(output, token);
                }
            })
            {
                FileDownloadName = fileName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Budget report export failed");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
        }
    }

    private static decimal ComputeBudgetedAmountForPeriod(IReadOnlyList<BudgetRule> rules, DateOnly from, DateOnly to)
    {
        if (rules == null || rules.Count == 0)
        {
            return 0m;
        }

        decimal sum = 0m;
        foreach (var rule in rules)
        {
            var step = rule.Interval switch
            {
                BudgetIntervalType.Monthly => 1,
                BudgetIntervalType.Quarterly => 3,
                BudgetIntervalType.Yearly => 12,
                BudgetIntervalType.CustomMonths => rule.CustomIntervalMonths ?? 1,
                _ => 1
            };

            var occ = rule.StartDate;
            var ruleEnd = rule.EndDate ?? to;

            while (occ < from)
            {
                occ = occ.AddMonths(step);
                if (occ > ruleEnd)
                {
                    break;
                }
            }

            while (occ <= to && occ <= ruleEnd)
            {
                sum += rule.Amount;
                occ = occ.AddMonths(step);
            }
        }

        return sum;
    }

    /// <summary>
    /// Generates a budget report for the current user.
    /// </summary>
    /// <param name="dateBasis">Whether to base the report on booking date or valuta date.</param>
    /// <param name="date">The date for which the KPI should be calculated. If not provided, the current date is used.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Returns planned/actual income and expenses for the Home Monthly Budget KPI.
    /// </returns>
    [HttpGet("kpi-monthly")]
    [ProducesResponseType(typeof(MonthlyBudgetKpiDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonthlyKpiAsync([FromQuery] DateOnly? date = null, [FromQuery] BudgetReportDateBasis dateBasis = BudgetReportDateBasis.ValutaDate, CancellationToken ct = default)
    {
        try
        {
            var kpi = await _reports.GetMonthlyKpiAsync(_current.UserId, date, dateBasis, ct);
            return Ok(kpi);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Get monthly budget KPI request invalid");
            return BadRequest(ApiErrorFactory.FromArgumentException(Origin, ex, _localizer));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get monthly budget KPI failed");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
        }
    }
    /// <summary>
    /// Generates a budget report for the current user.
    /// </summary>
    /// <param name="req">The report request parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="IActionResult"/> containing the generated <see cref="BudgetReportDto"/> or a validation/error result.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(BudgetReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAsync([FromBody] BudgetReportRequest req, CancellationToken ct = default)
    {
        try
        {
            if (req.Months < 1 || req.Months > 60)
            {
                var ex = new ArgumentOutOfRangeException(nameof(req.Months), "Months must be 1..60");
                return BadRequest(ApiErrorFactory.FromArgumentOutOfRangeException(Origin, ex, _localizer));
            }

            // Determine inclusive range: use AsOfDate as month anchor (end of month)
            var to = new DateOnly(req.AsOfDate.Year, req.AsOfDate.Month, DateTime.DaysInMonth(req.AsOfDate.Year, req.AsOfDate.Month));
            var from = new DateOnly(to.Year, to.Month, 1).AddMonths(-(req.Months - 1));

            // Retrieve raw data for the requested range and date basis
            var raw = await _reports.GetRawDataAsync(_current.UserId, from, to, req.DateBasis, ct);

            // Helper: choose date field according to request date basis
            static DateTime GetDate(BudgetReportPostingRawDataDto p, BudgetReportDateBasis basis)
                => basis == BudgetReportDateBasis.ValutaDate ? (p.ValutaDate ?? p.BookingDate) : p.BookingDate;

            // Build periods (monthly only supported for now)
            var rules = await _db.BudgetRules
                .AsNoTracking()
                .Where(r => r.OwnerUserId == _current.UserId)
                .ToListAsync(ct);

            var periods = new List<BudgetReportPeriodDto>(req.Months);
            for (int i = 0; i < req.Months; i++)
            {
                var periodFrom = new DateOnly(from.Year, from.Month, 1).AddMonths(i);
                var periodTo = new DateOnly(periodFrom.Year, periodFrom.Month, DateTime.DaysInMonth(periodFrom.Year, periodFrom.Month));

                // sum actual across all postings (categorized + uncategorized + unbudgeted)
                decimal actual = 0m;

                // categorized postings
                foreach (var cat in raw.Categories ?? Array.Empty<BudgetReportCategoryRawDataDto>())
                {
                    foreach (var pur in cat.Purposes ?? Array.Empty<BudgetReportPurposeRawDataDto>())
                    {
                        foreach (var p in pur.Postings ?? Array.Empty<BudgetReportPostingRawDataDto>())
                        {
                            var d = GetDate(p, req.DateBasis);
                            var dd = DateOnly.FromDateTime(d);
                            if (dd >= periodFrom && dd <= periodTo) actual += p.Amount;
                        }
                    }
                }

                // uncategorized purposes
                foreach (var pur in raw.UncategorizedPurposes ?? Array.Empty<BudgetReportPurposeRawDataDto>())
                {
                    foreach (var p in pur.Postings ?? Array.Empty<BudgetReportPostingRawDataDto>())
                    {
                        var d = GetDate(p, req.DateBasis);
                        var dd = DateOnly.FromDateTime(d);
                        if (dd >= periodFrom && dd <= periodTo) actual += p.Amount;
                    }
                }

                // unbudgeted postings
                foreach (var p in raw.UnbudgetedPostings ?? Array.Empty<BudgetReportPostingRawDataDto>())
                {
                    var d = GetDate(p, req.DateBasis);
                    var dd = DateOnly.FromDateTime(d);
                    if (dd >= periodFrom && dd <= periodTo) actual += p.Amount;
                }

                var budget = ComputeBudgetedAmountForPeriod(rules, periodFrom, periodTo);
                var delta = budget - actual;
                var deltaPct = budget == 0m ? 0m : delta / Math.Abs(budget);

                periods.Add(new BudgetReportPeriodDto(periodFrom, periodTo, budget, actual, delta, deltaPct));
            }

            // Build categories and purposes
            var categories = new List<BudgetReportCategoryDto>();
            var lastPeriod = periods.Last();
            var categoryFrom = req.CategoryValueScope == BudgetReportValueScope.TotalRange ? from : lastPeriod.From;
            var categoryTo = req.CategoryValueScope == BudgetReportValueScope.TotalRange ? to : lastPeriod.To;
            var unbudgetedPostings = raw.UnbudgetedPostings ?? Array.Empty<BudgetReportPostingRawDataDto>();

            bool IsInCategoryRange(BudgetReportPostingRawDataDto posting)
            {
                var dd = DateOnly.FromDateTime(GetDate(posting, req.DateBasis));
                return dd >= categoryFrom && dd <= categoryTo;
            }

            foreach (var cat in raw.Categories ?? Array.Empty<BudgetReportCategoryRawDataDto>())
            {
                var categoryRules = rules.Where(r => r.BudgetCategoryId == cat.CategoryId).ToList();
                decimal catBudget = ComputeBudgetedAmountForPeriod(categoryRules, categoryFrom, categoryTo);
                decimal catActual = 0m;
                var postingsToConsider = new List<BudgetReportPostingRawDataDto>();
                foreach (var pur in cat.Purposes ?? Array.Empty<BudgetReportPurposeRawDataDto>())
                {
                    postingsToConsider.AddRange((pur.Postings ?? Array.Empty<BudgetReportPostingRawDataDto>())
                        .Where(p =>
                        {
                            var d = GetDate(p, req.DateBasis);
                            var dd = DateOnly.FromDateTime(d);
                            return dd >= categoryFrom && dd <= categoryTo;
                        }));
                }

                catActual = postingsToConsider.Sum(p => p.Amount)
                    + unbudgetedPostings.Where(p => p.BudgetCategoryId == cat.CategoryId)
                        .Where(IsInCategoryRange)
                        .Sum(p => p.Amount);

                var purposeDtos = new List<BudgetReportPurposeDto>();
                foreach (var pur in cat.Purposes ?? Array.Empty<BudgetReportPurposeRawDataDto>())
                {
                    var purposeRules = rules.Where(r => r.BudgetPurposeId == pur.PurposeId).ToList();
                    decimal purBudget = ComputeBudgetedAmountForPeriod(purposeRules, categoryFrom, categoryTo);
                    decimal purActual = (pur.Postings ?? Array.Empty<BudgetReportPostingRawDataDto>())
                        .Where(p =>
                        {
                            var dd = DateOnly.FromDateTime(GetDate(p, req.DateBasis));
                            return dd >= categoryFrom && dd <= categoryTo;
                        })
                        .Sum(p => p.Amount);

                    purActual += unbudgetedPostings.Where(p => p.BudgetPurposeId == pur.PurposeId)
                        .Where(IsInCategoryRange)
                        .Sum(p => p.Amount);

                    purposeDtos.Add(new BudgetReportPurposeDto(
                        pur.PurposeId,
                        pur.PurposeName,
                        purBudget,
                        purActual,
                        purBudget - purActual,
                        purBudget == 0 ? 0m : (purBudget - purActual) / Math.Abs(purBudget),
                        pur.BudgetSourceType,
                        pur.SourceId));
                }

                categories.Add(new BudgetReportCategoryDto(
                    cat.CategoryId,
                    cat.CategoryName,
                    BudgetReportCategoryRowKind.Data,
                    catBudget,
                    catActual,
                    catBudget - catActual,
                    catBudget == 0 ? 0m : (catBudget - catActual) / Math.Abs(catBudget),
                    purposeDtos));
            }

            if (categories.Count > 0)
            {
                var sumBudget = categories.Sum(c => c.Budget);
                var sumActual = categories.Sum(c => c.Actual);
                var sumDelta = sumBudget - sumActual;
                var sumDeltaPct = sumBudget == 0m ? 0m : sumDelta / Math.Abs(sumBudget);

                categories.Add(new BudgetReportCategoryDto(
                    Guid.Empty,
                    "Sum",
                    BudgetReportCategoryRowKind.Sum,
                    sumBudget,
                    sumActual,
                    sumDelta,
                    sumDeltaPct,
                    Array.Empty<BudgetReportPurposeDto>()));
            }

            var result = new BudgetReportDto(from, to, req.Interval, periods, categories);
            return Ok(result);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ApiErrorFactory.FromArgumentOutOfRangeException(Origin, ex, _localizer));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiErrorFactory.FromArgumentException(Origin, ex, _localizer));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get budget report failed");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
        }
    }

    /// <summary>
    /// Lists postings that are not covered by any budget purpose for the given date range.
    /// </summary>
    [HttpGet("unbudgeted")]
    [ProducesResponseType(typeof(IReadOnlyList<PostingServiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnbudgetedAsync(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] BudgetReportDateBasis dateBasis = BudgetReportDateBasis.BookingDate,
        [FromQuery] string? kind = null,
        CancellationToken ct = default)
    {
        try
        {
            // Determine range boundaries
            var fromDt = (from ?? DateTime.MinValue).Date;
            var toDt = (to ?? DateTime.MaxValue);

            var ownerUserId = _current.UserId;

            // Load all purposes in date range to identify covered sources.
            var purposes = await _purposes.ListOverviewAsync(
                ownerUserId,
                skip: 0,
                take: 5000,
                sourceType: null,
                nameFilter: null,
                from: DateOnly.FromDateTime(fromDt),
                to: DateOnly.FromDateTime(toDt),
                budgetCategoryId: null,
                ct: ct,
                dateBasis: dateBasis);

            // Build a set of covered posting ids.
            var coveredPostingIds = new HashSet<Guid>();

            // Contact purposes: direct covered contacts
            var coveredContacts = purposes
                .Where(p => p.SourceType == BudgetSourceType.Contact)
                .Select(p => p.SourceId)
                .Distinct()
                .ToList();

            // SavingsPlan purposes: covered by savings plan postings
            var coveredSavingsPlans = purposes
                .Where(p => p.SourceType == BudgetSourceType.SavingsPlan)
                .Select(p => p.SourceId)
                .Distinct()
                .ToList();

            // ContactGroup purposes: covered contacts are contacts that belong to those groups
            var groupIds = purposes
                .Where(p => p.SourceType == BudgetSourceType.ContactGroup)
                .Select(p => p.SourceId)
                .Distinct()
                .ToList();

            if (groupIds.Count > 0)
            {
                var groupContactIds = await _db.Contacts.AsNoTracking()
                    .Where(c => c.OwnerUserId == ownerUserId && c.CategoryId != null && groupIds.Contains(c.CategoryId.Value))
                    .Select(c => c.Id)
                    .Distinct()
                    .ToListAsync(ct);
                coveredContacts.AddRange(groupContactIds);
                coveredContacts = coveredContacts.Distinct().ToList();
            }

            // Overlap rule: if there is a self-contact purpose, savings plan postings are covered by self contact.
            var selfContactId = await _db.Contacts.AsNoTracking()
                .Where(c => c.OwnerUserId == ownerUserId && c.Type == FinanceManager.Shared.Dtos.Contacts.ContactType.Self)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct);
            if (selfContactId.HasValue && coveredContacts.Contains(selfContactId.Value))
            {
                coveredSavingsPlans.Clear();
            }

            // Query covered posting ids.
            var coveredQuery = _db.Postings.AsNoTracking().Where(p => true);
            coveredQuery = dateBasis == BudgetReportDateBasis.ValutaDate
                ? coveredQuery.Where(p => p.ValutaDate != null && p.ValutaDate >= fromDt && p.ValutaDate <= toDt)
                : coveredQuery.Where(p => p.BookingDate >= fromDt && p.BookingDate <= toDt);

            if (coveredContacts.Count > 0)
            {
                var ids = await coveredQuery
                    .Where(p => p.ContactId != null && coveredContacts.Contains(p.ContactId.Value))
                    .Select(p => p.Id)
                    .ToListAsync(ct);
                foreach (var id in ids)
                {
                    coveredPostingIds.Add(id);
                }
            }

            if (coveredSavingsPlans.Count > 0)
            {
                var ids = await coveredQuery
                    .Where(p => p.SavingsPlanId != null && coveredSavingsPlans.Contains(p.SavingsPlanId.Value))
                    .Select(p => p.Id)
                    .ToListAsync(ct);
                foreach (var id in ids)
                {
                    coveredPostingIds.Add(id);
                }

                // Also mark mirrored contact postings as covered.
                // Savings plan postings are often duplicated as contact postings within the same group.
                // If a savings plan posting is covered, the corresponding contact posting in the same group must be treated as covered as well.
                var coveredSavingsGroupIds = await coveredQuery
                    .Where(p => p.SavingsPlanId != null && coveredSavingsPlans.Contains(p.SavingsPlanId.Value) && p.GroupId != Guid.Empty)
                    .Select(p => p.GroupId)
                    .Distinct()
                    .ToListAsync(ct);

                if (coveredSavingsGroupIds.Count > 0)
                {
                    var mirroredContactPostingIds = await coveredQuery
                        .Where(p => p.ContactId != null && p.SavingsPlanId == null && coveredSavingsGroupIds.Contains(p.GroupId))
                        .Select(p => p.Id)
                        .ToListAsync(ct);

                    foreach (var id in mirroredContactPostingIds)
                    {
                        coveredPostingIds.Add(id);
                    }
                }
            }

            // Load all contact postings in range and filter out covered ids.
            var allContactQuery = _db.Postings.AsNoTracking().Where(p => p.ContactId != null && p.SavingsPlanId == null);
            allContactQuery = dateBasis == BudgetReportDateBasis.ValutaDate
                ? allContactQuery.Where(p => p.ValutaDate != null && p.ValutaDate >= fromDt && p.ValutaDate <= toDt)
                : allContactQuery.Where(p => p.BookingDate >= fromDt && p.BookingDate <= toDt);

            // Optional split for UI: cost-neutral self-contact mirror postings vs remaining.
            // "selfCostNeutral": self-contact, grouped postings (GroupId != empty)
            // "remaining": everything else
            if (!string.IsNullOrWhiteSpace(kind))
            {
                var selfId = await _db.Contacts.AsNoTracking()
                    .Where(c => c.OwnerUserId == ownerUserId && c.Type == FinanceManager.Shared.Dtos.Contacts.ContactType.Self)
                    .Select(c => (Guid?)c.Id)
                    .FirstOrDefaultAsync(ct);

                if (kind.Equals("selfCostNeutral", StringComparison.OrdinalIgnoreCase))
                {
                    if (selfId.HasValue)
                    {
                        allContactQuery = allContactQuery.Where(p => p.ContactId == selfId.Value && p.GroupId != Guid.Empty);
                    }
                    else
                    {
                        allContactQuery = allContactQuery.Where(_ => false);
                    }
                }
                else if (kind.Equals("remaining", StringComparison.OrdinalIgnoreCase))
                {
                    if (selfId.HasValue)
                    {
                        allContactQuery = allContactQuery.Where(p => p.ContactId != selfId.Value || p.GroupId == Guid.Empty);
                    }
                }
            }

            var unbudgetedPostings = await allContactQuery
                .Where(p => !coveredPostingIds.Contains(p.Id))
                .OrderByDescending(p => dateBasis == BudgetReportDateBasis.ValutaDate ? p.ValutaDate : p.BookingDate)
                .Take(5000)
                .ToListAsync(ct);

            var unbudgeted = unbudgetedPostings
                .Select(p => new PostingServiceDto(
                    p.Id,
                    p.BookingDate,
                    p.ValutaDate,
                    p.Amount,
                    p.Kind,
                    p.AccountId,
                    p.ContactId,
                    p.SavingsPlanId,
                    p.SecurityId,
                    p.SourceId,
                    p.Subject,
                    p.RecipientName,
                    p.Description,
                    p.SecuritySubType,
                    p.Quantity,
                    p.GroupId,
                    p.LinkedPostingId,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null))
                .ToList();

            return Ok(unbudgeted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get unbudgeted postings failed");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
        }
    }
}

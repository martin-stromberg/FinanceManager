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
    private readonly IReportCacheService _cacheService;

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
        IBudgetReportExportService export,
        IReportCacheService cacheService)
    {
        _reports = reports;
        _purposes = purposes;
        _db = db;
        _current = current;
        _logger = logger;
        _localizer = localizer;
        _export = export;
        _cacheService = cacheService;
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

    /// <summary>
    /// Clears cached report data for the current user.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="IActionResult"/> indicating success.</returns>
    [HttpPost("cache/reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetCacheAsync(CancellationToken ct = default)
    {
        await _cacheService.ClearReportCacheAsync(_current.UserId, ct);
        return NoContent();
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

    private static decimal ComputeDelta(decimal budget, decimal actual)
        => actual - budget;

    private static decimal ComputeDeltaPct(decimal budget, decimal delta)
        => budget == 0m ? 0m : delta / Math.Abs(budget);

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
            var raw = await _reports.GetRawDataAsync(_current.UserId, from, to, req.DateBasis, ct, ignoreCache: true);

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
                        foreach (var p in (pur.Postings ?? Array.Empty<BudgetReportPostingRawDataDto>()).Where(p => p.IsValuedForBudgetPurpose))
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
                    foreach (var p in (pur.Postings ?? Array.Empty<BudgetReportPostingRawDataDto>()).Where(p => p.IsValuedForBudgetPurpose))
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
                var delta = ComputeDelta(budget, actual);
                var deltaPct = ComputeDeltaPct(budget, delta);

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
                        .Where(p => p.IsValuedForBudgetPurpose)
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
                    catBudget += purBudget;
                    decimal purActual = (pur.Postings ?? Array.Empty<BudgetReportPostingRawDataDto>())
                        .Where(p => p.IsValuedForBudgetPurpose)
                        .Where(p =>
                        {
                            var dd = DateOnly.FromDateTime(GetDate(p, req.DateBasis));
                            return dd >= categoryFrom && dd <= categoryTo;
                        })
                        .Sum(p => p.Amount);

                    purActual += unbudgetedPostings.Where(p => p.BudgetPurposeId == pur.PurposeId)
                        .Where(IsInCategoryRange)
                        .Sum(p => p.Amount);

                    var purDelta = ComputeDelta(purBudget, purActual);

                    purposeDtos.Add(new BudgetReportPurposeDto(
                        pur.PurposeId,
                        pur.PurposeName,
                        purBudget,
                        purActual,
                        purDelta,
                        ComputeDeltaPct(purBudget, purDelta),
                        pur.BudgetSourceType,
                        pur.SourceId));
                }

                var catDelta = ComputeDelta(catBudget, catActual);

                categories.Add(new BudgetReportCategoryDto(
                    cat.CategoryId,
                    cat.CategoryName,
                    BudgetReportCategoryRowKind.Data,
                    catBudget,
                    catActual,
                    catDelta,
                    ComputeDeltaPct(catBudget, catDelta),
                    purposeDtos));
            }

            var unbudgetedActual = unbudgetedPostings
                .Where(p => p.BudgetPurposeId == null || p.BudgetPurposeId == Guid.Empty)
                .Where(IsInCategoryRange)
                .Sum(p => p.Amount);

            if (unbudgetedActual != 0m)
            {
                categories.Add(new BudgetReportCategoryDto(
                    Guid.Empty,
                    "Unbudgeted",
                    BudgetReportCategoryRowKind.Unbudgeted,
                    0m,
                    unbudgetedActual,
                    unbudgetedActual,
                    0m,
                    Array.Empty<BudgetReportPurposeDto>()));
            }

            if (categories.Count > 0)
            {
                var sumBudget = categories.Sum(c => c.Budget);
                var sumActual = categories.Sum(c => c.Actual);
                var sumDelta = ComputeDelta(sumBudget, sumActual);
                var sumDeltaPct = ComputeDeltaPct(sumBudget, sumDelta);

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
    /// Returns raw budget report data for UI scenarios that need posting-level budget valuation state.
    /// </summary>
    /// <param name="req">The report request parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="IActionResult"/> containing raw budget report data.</returns>
    [HttpPost("raw")]
    [ProducesResponseType(typeof(BudgetReportRawDataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRawAsync([FromBody] BudgetReportRequest req, CancellationToken ct = default)
    {
        try
        {
            if (req.Months < 1 || req.Months > 60)
            {
                var ex = new ArgumentOutOfRangeException(nameof(req.Months), "Months must be 1..60");
                return BadRequest(ApiErrorFactory.FromArgumentOutOfRangeException(Origin, ex, _localizer));
            }

            var to = new DateOnly(req.AsOfDate.Year, req.AsOfDate.Month, DateTime.DaysInMonth(req.AsOfDate.Year, req.AsOfDate.Month));
            var from = new DateOnly(to.Year, to.Month, 1).AddMonths(-(req.Months - 1));
            var raw = await _reports.GetRawDataAsync(_current.UserId, from, to, req.DateBasis, ct, ignoreCache: true);
            return Ok(raw);
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
            _logger.LogError(ex, "Get raw budget report failed");
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
            var fromDate = DateOnly.FromDateTime(fromDt);
            var toDate = DateOnly.FromDateTime(toDt);

            // Use GetRawDataAsync to get properly filtered posting IDs that respect pattern matching
            var raw = await _reports.GetRawDataAsync(ownerUserId, fromDate, toDate, dateBasis, ct, ignoreCache: true);

            var unbudgetedPostingIds = (raw.UnbudgetedPostings ?? Array.Empty<BudgetReportPostingRawDataDto>())
                .Select(p => p.PostingId)
                .ToHashSet();

            if (unbudgetedPostingIds.Count == 0)
            {
                return Ok(new List<PostingServiceDto>());
            }

            // Load full posting details for mapping
            var allPostingsQuery = _db.Postings.AsNoTracking();
            allPostingsQuery = dateBasis == BudgetReportDateBasis.ValutaDate
                ? allPostingsQuery.Where(p => p.ValutaDate != null && p.ValutaDate >= fromDt && p.ValutaDate <= toDt)
                : allPostingsQuery.Where(p => p.BookingDate >= fromDt && p.BookingDate <= toDt);

            var postings = await allPostingsQuery
                .Where(p => unbudgetedPostingIds.Contains(p.Id))
                .OrderByDescending(p => dateBasis == BudgetReportDateBasis.ValutaDate ? p.ValutaDate : p.BookingDate)
                .Take(5000)
                .ToListAsync(ct);

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
                        postings = postings
                            .Where(p => p.ContactId == selfId.Value && p.GroupId != Guid.Empty)
                            .ToList();
                    }
                    else
                    {
                        postings.Clear();
                    }
                }
                else if (kind.Equals("remaining", StringComparison.OrdinalIgnoreCase))
                {
                    if (selfId.HasValue)
                    {
                        postings = postings
                            .Where(p => p.ContactId != selfId.Value || p.GroupId == Guid.Empty)
                            .ToList();
                    }
                }
            }

            var unbudgeted = postings
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
                    null,
                    p.IsReversed,
                    p.IsReversal,
                    p.ReversedByPostingId,
                    p.ReversalForPostingId))
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

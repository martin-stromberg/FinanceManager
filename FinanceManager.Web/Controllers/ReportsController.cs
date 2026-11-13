using FinanceManager.Application;
using FinanceManager.Application.Common;
using FinanceManager.Application.Reports;
using FinanceManager.Shared.Dtos.Common;
using FinanceManager.Web.Infrastructure.ApiErrors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Net.Mime;

namespace FinanceManager.Web.Controllers;

/// <summary>
/// Provides reporting endpoints: aggregation queries and management of report favorites.
/// </summary>
[ApiController]
[Route("api")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ReportsController : ControllerBase
{
    private const string Origin = "API_Reports";

    private readonly IReportAggregationService _agg;
    private readonly IReportPostingsQueryService _postings;
    private readonly IReportFavoriteService _favorites;
    private readonly ICurrentUserService _current;
    private readonly ILogger<ReportsController> _logger;
    private readonly IStringLocalizer<Controller> _localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsController"/> class.
    /// </summary>
    /// <param name="agg">Service that executes aggregation queries.</param>
    /// <param name="postings">Service that resolves the postings behind a single report cell.</param>
    /// <param name="favorites">Service that manages report favorites.</param>
    /// <param name="current">Service providing the current user context.</param>
    /// <param name="logger">Logger instance for this controller.</param>
    /// <param name="localizer">Localizer for error messages.</param>
    public ReportsController(
        IReportAggregationService agg,
        IReportPostingsQueryService postings,
        IReportFavoriteService favorites,
        ICurrentUserService current,
        ILogger<ReportsController> logger,
        IStringLocalizer<Controller> localizer)
    {
        _agg = agg;
        _postings = postings;
        _favorites = favorites;
        _current = current;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Executes a report aggregation query returning interval-based totals and optional comparisons.
    /// Supports multi-kind fallback via query string (?postingKinds=0,1,2) and optional analysis date.
    /// </summary>
    /// <param name="req">Aggregation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing a 200 OK and the <see cref="ReportAggregationResult"/> when successful,
    /// or a 400 Bad Request when validation fails.
    /// </returns>
    /// <exception cref="Exception">An unexpected error that is logged and results in a 500 Internal Server Error response.</exception>
    /// <response code="200">The HTTP 200 response.</response>
    /// <response code="400">The HTTP 400 response.</response>
    [HttpPost("report-aggregates")]
    [ProducesResponseType(typeof(ReportAggregationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> QueryAsync([FromBody] ReportAggregatesQueryRequest req, CancellationToken ct)
    {
        try
        {
            if (req.Take < 1 || req.Take > 200)
            {
                var ex = new ArgumentOutOfRangeException(nameof(req.Take), "Take must be 1..200");
                return BadRequest(ApiErrorFactory.FromArgumentOutOfRangeException(Origin, ex, _localizer));
            }
            IReadOnlyCollection<PostingKind>? multi = req.PostingKinds;
            if (multi == null || multi.Count == 0)
            {
                var kindsParam = Request.Query["postingKinds"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(kindsParam))
                {
                    multi = kindsParam.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(s => int.TryParse(s, out var v) ? v : (int?)null)
                        .Where(v => v.HasValue)
                        .Select(v => (PostingKind)v!.Value)
                        .Distinct().ToArray();
                }
            }
            DateTime? analysisDate = req.AnalysisDate;
            if (!analysisDate.HasValue)
            {
                var adStr = Request.Query["analysisDate"].FirstOrDefault();
                if (DateTime.TryParse(adStr, out var ad)) { analysisDate = new DateTime(ad.Year, ad.Month, 1); }
            }
            else
            {
                var d = analysisDate.Value.Date; analysisDate = new DateTime(d.Year, d.Month, 1);
            }
            ReportAggregationFilters? filters = null;
            if (req.Filters != null)
            {
                filters = new ReportAggregationFilters(
                    AccountIds: req.Filters.AccountIds,
                    ContactIds: req.Filters.ContactIds,
                    SavingsPlanIds: req.Filters.SavingsPlanIds,
                    SecurityIds: req.Filters.SecurityIds,
                    ContactCategoryIds: req.Filters.ContactCategoryIds,
                    SavingsPlanCategoryIds: req.Filters.SavingsPlanCategoryIds,
                    SecurityCategoryIds: req.Filters.SecurityCategoryIds,
                    SecuritySubTypes: req.Filters.SecuritySubTypes,
                    IncludeDividendRelated: req.Filters.IncludeDividendRelated,
                    BankTextFilters: req.Filters.BankTextFilters,
                    ContactTextFilters: req.Filters.ContactTextFilters,
                    SavingsPlanTextFilters: req.Filters.SavingsPlanTextFilters,
                    SecurityTextFilters: req.Filters.SecurityTextFilters);
            }
            var query = new ReportAggregationQuery(_current.UserId, req.PostingKind, req.Interval, req.Take, req.IncludeCategory, req.ComparePrevious, req.CompareYear, req.CompareProjection, multi, analysisDate, req.UseValutaDate, filters, req.SavingsPlanPostingSource);
            var result = await _agg.QueryAsync(query, ct);
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
            _logger.LogError(ex, "Report aggregation failed");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
        }
    }

    private static readonly string[] GroupKeyPrefixes = { "Account:", "Contact:", "SavingsPlan:", "Security:", "Category:", "Type:" };

    /// <summary>
    /// Lists the individual postings that compose a single report cell (drill-down).
    /// Reproduces the aggregation's ownership, filter and comparison-period semantics.
    /// </summary>
    /// <param name="req">Postings query request (cell coordinates plus aggregation context).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing a 200 OK and the <see cref="ReportPostingsResult"/> when successful,
    /// or a 400 Bad Request when validation fails.
    /// </returns>
    /// <response code="200">The HTTP 200 response.</response>
    /// <response code="400">The HTTP 400 response.</response>
    [HttpPost("report-postings")]
    [ProducesResponseType(typeof(ReportPostingsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> QueryPostingsAsync([FromBody] ReportPostingsQueryRequest req, CancellationToken ct)
    {
        try
        {
            if (!Enum.IsDefined(req.Period))
            {
                var ex = new ArgumentOutOfRangeException(nameof(req.Period), req.Period, "Unknown postings period.");
                return BadRequest(ApiErrorFactory.FromArgumentOutOfRangeException(Origin, ex, _localizer));
            }
            if (!Enum.IsDefined(req.Interval))
            {
                var ex = new ArgumentOutOfRangeException(nameof(req.Interval), req.Interval, "Unknown report interval.");
                return BadRequest(ApiErrorFactory.FromArgumentOutOfRangeException(Origin, ex, _localizer));
            }
            if (!Enum.IsDefined(req.PostingKind))
            {
                var ex = new ArgumentOutOfRangeException(nameof(req.PostingKind), req.PostingKind, "Unknown posting kind.");
                return BadRequest(ApiErrorFactory.FromArgumentOutOfRangeException(Origin, ex, _localizer));
            }
            if (req.PostingKinds is { Count: > 0 } kinds && kinds.Any(k => !Enum.IsDefined(k)))
            {
                var ex = new ArgumentOutOfRangeException(nameof(req.PostingKinds), "Unknown posting kind in multi selection.");
                return BadRequest(ApiErrorFactory.FromArgumentOutOfRangeException(Origin, ex, _localizer));
            }
            if (!Enum.IsDefined(req.SavingsPlanPostingSource))
            {
                var ex = new ArgumentOutOfRangeException(nameof(req.SavingsPlanPostingSource), req.SavingsPlanPostingSource, "Unknown savings plan posting source.");
                return BadRequest(ApiErrorFactory.FromArgumentOutOfRangeException(Origin, ex, _localizer));
            }
            if (req.Take < 0)
            {
                var ex = new ArgumentOutOfRangeException(nameof(req.Take), req.Take, "Take must not be negative.");
                return BadRequest(ApiErrorFactory.FromArgumentOutOfRangeException(Origin, ex, _localizer));
            }
            if (req.GroupKey != null && !GroupKeyPrefixes.Any(p => req.GroupKey.StartsWith(p, StringComparison.Ordinal)))
            {
                var ex = new ArgumentException("Invalid group key.", nameof(req.GroupKey));
                return BadRequest(ApiErrorFactory.FromArgumentException(Origin, ex, _localizer));
            }
            if (req.Interval == ReportInterval.AllHistory && req.Period != ReportPostingsPeriod.Current)
            {
                var ex = new ArgumentException("Comparison periods are not available for the all-history interval.", nameof(req.Period));
                return BadRequest(ApiErrorFactory.FromArgumentException(Origin, ex, _localizer));
            }
            ReportAggregationFilters? filters = null;
            if (req.Filters != null)
            {
                filters = new ReportAggregationFilters(
                    AccountIds: req.Filters.AccountIds,
                    ContactIds: req.Filters.ContactIds,
                    SavingsPlanIds: req.Filters.SavingsPlanIds,
                    SecurityIds: req.Filters.SecurityIds,
                    ContactCategoryIds: req.Filters.ContactCategoryIds,
                    SavingsPlanCategoryIds: req.Filters.SavingsPlanCategoryIds,
                    SecurityCategoryIds: req.Filters.SecurityCategoryIds,
                    SecuritySubTypes: req.Filters.SecuritySubTypes,
                    IncludeDividendRelated: req.Filters.IncludeDividendRelated,
                    BankTextFilters: req.Filters.BankTextFilters,
                    ContactTextFilters: req.Filters.ContactTextFilters,
                    SavingsPlanTextFilters: req.Filters.SavingsPlanTextFilters,
                    SecurityTextFilters: req.Filters.SecurityTextFilters);
            }
            var query = new ReportPostingsQuery(_current.UserId, req.GroupKey, req.PeriodStart, req.Period, req.PostingKind, req.Interval, req.IncludeCategory, req.CompareProjection, req.UseValutaDate, req.PostingKinds, req.AnalysisDate, filters, req.SavingsPlanPostingSource, req.Take, req.CompareYear);
            var result = await _postings.QueryAsync(query, ct);
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
            _logger.LogError(ex, "Report postings query failed");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
        }
    }

    /// <summary>
    /// Lists all report favorites for the current user.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="IActionResult"/> containing a 200 OK response with a list of <see cref="ReportFavoriteDto"/>.</returns>
    /// <response code="200">The HTTP 200 response.</response>
    [HttpGet("report-favorites")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportFavoriteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFavoritesAsync(CancellationToken ct)
        => Ok(await _favorites.ListAsync(_current.UserId, ct));

    /// <summary>
    /// Gets a single report favorite by id.
    /// </summary>
    /// <param name="id">Favorite id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> that contains a <see cref="ReportFavoriteDto"/> and a 200 OK status when found,
    /// or a 404 Not Found when the favorite does not exist or does not belong to the current user.
    /// </returns>
    /// <response code="200">The HTTP 200 response.</response>
    /// <response code="404">The HTTP 404 response.</response>
    [HttpGet("report-favorites/{id:guid}", Name = "GetReportFavorite")]
    [ProducesResponseType(typeof(ReportFavoriteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFavoriteAsync(Guid id, CancellationToken ct)
    {
        var dto = await _favorites.GetAsync(id, _current.UserId, ct);
        return dto == null ? NotFound() : Ok(dto);
    }

    /// <summary>
    /// Creates a new report favorite definition.
    /// </summary>
    /// <param name="req">Creation request payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="IActionResult"/> with 201 Created and the created <see cref="ReportFavoriteDto"/> on success.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a favorite with the same criteria already exists (mapped to 409 Conflict).</exception>
    /// <exception cref="ArgumentException">Thrown when the request contains invalid data (mapped to 400 Bad Request).</exception>
    /// <exception cref="Exception">Unexpected errors are logged and result in a 500 Internal Server Error.</exception>
    /// <response code="201">The HTTP 201 response.</response>
    /// <response code="400">The HTTP 400 response.</response>
    /// <response code="409">The HTTP 409 response.</response>
    [HttpPost("report-favorites")]
    [ProducesResponseType(typeof(ReportFavoriteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateFavoriteAsync([FromBody] ReportFavoriteCreateApiRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var filters = req.Filters == null ? null : new ReportFavoriteFiltersDto(
                AccountIds: req.Filters.AccountIds,
                ContactIds: req.Filters.ContactIds,
                SavingsPlanIds: req.Filters.SavingsPlanIds,
                SecurityIds: req.Filters.SecurityIds,
                ContactCategoryIds: req.Filters.ContactCategoryIds,
                SavingsPlanCategoryIds: req.Filters.SavingsPlanCategoryIds,
                SecurityCategoryIds: req.Filters.SecurityCategoryIds,
                SecuritySubTypes: req.Filters.SecuritySubTypes,
                IncludeDividendRelated: req.Filters.IncludeDividendRelated,
                BankTextFilters: req.Filters.BankTextFilters,
                ContactTextFilters: req.Filters.ContactTextFilters,
                SavingsPlanTextFilters: req.Filters.SavingsPlanTextFilters,
                SecurityTextFilters: req.Filters.SecurityTextFilters);
            var dto = await _favorites.CreateAsync(_current.UserId, new ReportFavoriteCreateRequest(req.Name.Trim(), req.PostingKind, req.IncludeCategory, (ReportInterval)req.Interval, req.Take, req.ComparePrevious, req.CompareYear, req.CompareProjection, req.ShowChart, req.Expandable, req.PostingKinds, filters, req.UseValutaDate, req.SavingsPlanPostingSource), ct);
            return CreatedAtRoute("GetReportFavorite", new { id = dto.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            const string code = "Err_Conflict_ReportsFavorite";
            var msgEntry = _localizer[$"{Origin}_{code}"];
            var message = msgEntry.ResourceNotFound ? ex.Message : msgEntry.Value;
            return Conflict(ApiErrorDto.Create(Origin, code, message));
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
            _logger.LogError(ex, "Create report favorite failed");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
        }
    }

    /// <summary>
    /// Updates an existing report favorite definition.
    /// </summary>
    /// <param name="id">Favorite id.</param>
    /// <param name="req">Update request payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="IActionResult"/> with 200 OK and the updated <see cref="ReportFavoriteDto"/>, or 404 when not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the update would conflict with existing favorites (mapped to 409 Conflict).</exception>
    /// <exception cref="ArgumentException">Thrown when the request contains invalid data (mapped to 400 Bad Request).</exception>
    /// <exception cref="Exception">Unexpected errors are logged and result in a 500 Internal Server Error.</exception>
    /// <response code="200">The HTTP 200 response.</response>
    /// <response code="400">The HTTP 400 response.</response>
    /// <response code="404">The HTTP 404 response.</response>
    /// <response code="409">The HTTP 409 response.</response>
    [HttpPut("report-favorites/{id:guid}")]
    [ProducesResponseType(typeof(ReportFavoriteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateFavoriteAsync(Guid id, [FromBody] ReportFavoriteUpdateApiRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var filters = req.Filters == null ? null : new ReportFavoriteFiltersDto(
                AccountIds: req.Filters.AccountIds,
                ContactIds: req.Filters.ContactIds,
                SavingsPlanIds: req.Filters.SavingsPlanIds,
                SecurityIds: req.Filters.SecurityIds,
                ContactCategoryIds: req.Filters.ContactCategoryIds,
                SavingsPlanCategoryIds: req.Filters.SavingsPlanCategoryIds,
                SecurityCategoryIds: req.Filters.SecurityCategoryIds,
                SecuritySubTypes: req.Filters.SecuritySubTypes,
                IncludeDividendRelated: req.Filters.IncludeDividendRelated,
                BankTextFilters: req.Filters.BankTextFilters,
                ContactTextFilters: req.Filters.ContactTextFilters,
                SavingsPlanTextFilters: req.Filters.SavingsPlanTextFilters,
                SecurityTextFilters: req.Filters.SecurityTextFilters);
            var dto = await _favorites.UpdateAsync(id, _current.UserId, new ReportFavoriteUpdateRequest(req.Name.Trim(), req.PostingKind, req.IncludeCategory, (ReportInterval)req.Interval, req.Take, req.ComparePrevious, req.CompareYear, req.CompareProjection, req.ShowChart, req.Expandable, req.PostingKinds, filters, req.UseValutaDate, req.SavingsPlanPostingSource), ct);
            return dto == null ? NotFound() : Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            const string code = "Err_Conflict_ReportsFavorite";
            var msgEntry = _localizer[$"{Origin}_{code}"];
            var message = msgEntry.ResourceNotFound ? ex.Message : msgEntry.Value;
            return Conflict(ApiErrorDto.Create(Origin, code, message));
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
            _logger.LogError(ex, "Update report favorite {FavoriteId} failed", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
        }
    }

    /// <summary>
    /// Deletes a report favorite.
    /// </summary>
    /// <param name="id">Favorite id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="IActionResult"/> with 204 No Content when deleted, or 404 Not Found when not found.</returns>
    /// <exception cref="Exception">Unexpected errors are logged and result in a 500 Internal Server Error.</exception>
    /// <response code="204">The HTTP 204 response.</response>
    /// <response code="404">The HTTP 404 response.</response>
    [HttpDelete("report-favorites/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFavoriteAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var ok = await _favorites.DeleteAsync(id, _current.UserId, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete report favorite {FavoriteId} failed", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
        }
    }
}

using FinanceManager.Application;
using FinanceManager.Application.Attachments;
using FinanceManager.Application.Reports;
using FinanceManager.Application.Securities;
using FinanceManager.Domain.Attachments;
using FinanceManager.Domain.Postings;
using FinanceManager.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Mime;
using FinanceManager.Application.Common;
using FinanceManager.Shared.Dtos.Common;
using FinanceManager.Web.Infrastructure.ApiErrors;
using Microsoft.Extensions.Localization;
using FinanceManager.Web.Controllers;

namespace FinanceManager.Web.Controllers;

/// <summary>
/// Manages securities: CRUD, symbol attachment, price queries, time series aggregates,
/// dividends aggregation and background price backfill tasks.
/// </summary>
[ApiController]
[Route("api/securities")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces(MediaTypeNames.Application.Json)]
public sealed class SecuritiesController : ControllerBase
{
    private const string Origin = "API_Securities";

    private readonly ISecurityService _service;
    private readonly ICurrentUserService _current;
    private readonly IAttachmentService _attachments;
    private readonly IBackgroundTaskManager _tasks;
    private readonly IPostingTimeSeriesService _series;
    private readonly ISecurityPriceService _priceService;
    private readonly ISecurityReportService _reports;
    private readonly ILogger<SecuritiesController> _logger;
    private readonly IParentAssignmentService _parentAssign;
    private readonly IStringLocalizer<Controller> _localizer;

    /// <summary>
    /// Initializes a new instance of <see cref="SecuritiesController"/>.
    /// </summary>
    /// <param name="service">Service providing business operations for securities.</param>
    /// <param name="current">Service that provides information about the current user.</param>
    /// <param name="attachments">Service managing attachments (upload, storage).</param>
    /// <param name="series">Service to compute posting time series and aggregates.</param>
    /// <param name="priceService">Service to retrieve security price history.</param>
    /// <param name="reports">Service providing security reports and aggregations.</param>
    /// <param name="tasks">Background task manager used to enqueue asynchronous background jobs.</param>
    /// <param name="logger">Logger used for diagnostic messages.</param>
    /// <param name="parentAssign">Service that manages server-side create-and-assign operations.</param>
    /// <param name="localizer">Localizer for generating user-friendly error messages.</param>
    public SecuritiesController(
        ISecurityService service,
        ICurrentUserService current,
        IAttachmentService attachments,
        IBackgroundTaskManager tasks,
        IPostingTimeSeriesService series,
        ISecurityPriceService priceService,
        ISecurityReportService reports,
        ILogger<SecuritiesController> logger,
        IParentAssignmentService parentAssign,
        IStringLocalizer<Controller> localizer)
    {
        _service = service;
        _current = current;
        _attachments = attachments;
        _tasks = tasks;
        _series = series;
        _priceService = priceService;
        _reports = reports;
        _logger = logger;
        _parentAssign = parentAssign;
        _localizer = localizer;
    }

    /// <summary>
    /// Lists securities for the current user.
    /// </summary>
    /// <param name="onlyActive">When true only active securities are returned; otherwise archived ones are included.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>200 OK with a list of <see cref="SecurityDto"/> objects.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SecurityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] bool onlyActive = true, CancellationToken ct = default)
        => Ok(await _service.ListAsync(_current.UserId, onlyActive, ct));

    /// <summary>
    /// Returns count of securities for the current user.
    /// </summary>
    /// <param name="onlyActive">When true only count active securities; otherwise count all.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>200 OK with an object containing the count.</returns>
    [HttpGet("count")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> CountAsync([FromQuery] bool onlyActive = true, CancellationToken ct = default)
        => Ok(new { count = await _service.CountAsync(_current.UserId, onlyActive, ct) });

    /// <summary>
    /// Gets a single security by id for the current user.
    /// </summary>
    /// <param name="id">Security identifier (GUID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with <see cref="SecurityDto"/> when found; 404 NotFound when the security doesn't exist or is not owned by the current user.</returns>
    [HttpGet("{id:guid}", Name = "GetSecurityAsync")]
    [ProducesResponseType(typeof(SecurityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await _service.GetAsync(id, _current.UserId, ct);
        return dto == null ? NotFound() : Ok(dto);
    }

    /// <summary>
    /// Creates a new security owned by the current user.
    /// </summary>
    /// <param name="req">Security create request payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the created <see cref="SecurityDto"/>, or 400 Bad Request when validation fails.</returns>
    /// <exception cref="ArgumentException">May be thrown by the service for invalid input; will be mapped to 400 Bad Request.</exception>
    [HttpPost]
    [ProducesResponseType(typeof(SecurityDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync([FromBody] SecurityRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) { return ValidationProblem(ModelState); }
        try
        {
            var dto = await _service.CreateAsync(_current.UserId, req.Name, req.Identifier, req.Description, req.AlphaVantageCode, req.CurrencyCode, req.CategoryId, ct);

            await _parentAssign.TryAssignAsync(
                _current.UserId,
                req.Parent,
                createdKind: "securities",
                createdId: dto.Id,
                ct);

            return CreatedAtRoute("GetSecurityAsync", new { id = dto.Id }, dto);
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
            _logger.LogError(ex, "Create security failed");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
        }
    }

    /// <summary>
    /// Updates an existing security.
    /// </summary>
    /// <param name="id">Security identifier.</param>
    /// <param name="req">Update request payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with updated <see cref="SecurityDto"/>, 404 NotFound when not found, or 400 BadRequest for invalid input.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SecurityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] SecurityRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) { return ValidationProblem(ModelState); }
        try
        {
            var dto = await _service.UpdateAsync(id, _current.UserId, req.Name, req.Identifier, req.Description, req.AlphaVantageCode, req.CurrencyCode, req.CategoryId, ct);
            return dto == null ? NotFound() : Ok(dto);
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
            _logger.LogError(ex, "Update security {SecurityId} failed", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
        }
    }

    /// <summary>
    /// Archives (soft-hides) a security so it is no longer shown in active lists.
    /// </summary>
    /// <param name="id">Security identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 NoContent on success; 404 NotFound when the security does not exist or is not owned by the user.</returns>
    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var ok = await _service.ArchiveAsync(id, _current.UserId, ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>
    /// Deletes a security permanently.
    /// </summary>
    /// <param name="id">Security identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 NoContent on success; 404 NotFound when the security does not exist or is not owned by the user.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var ok = await _service.DeleteAsync(id, _current.UserId, ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>
    /// Assigns a symbol attachment to a security.
    /// </summary>
    /// <param name="id">Security identifier.</param>
    /// <param name="attachmentId">Attachment identifier to set as symbol.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 NoContent on success; 404 NotFound when the security or attachment is invalid.</returns>
    [HttpPost("{id:guid}/symbol/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetSymbolAsync(Guid id, Guid attachmentId, CancellationToken ct)
    {
        try
        {
            await _service.SetSymbolAttachmentAsync(id, _current.UserId, attachmentId, ct);
            return NoContent();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ApiErrorFactory.FromArgumentOutOfRangeException(Origin, ex, _localizer));
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiErrorFactory.FromArgumentException(Origin, ex, _localizer));
        }
    }

    /// <summary>
    /// Clears a symbol attachment from the security.
    /// </summary>
    /// <param name="id">Security identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 NoContent on success; 404 NotFound when the security is invalid.</returns>
    [HttpDelete("{id:guid}/symbol")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClearSymbolAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await _service.SetSymbolAttachmentAsync(id, _current.UserId, null, ct);
            return NoContent();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ApiErrorFactory.FromArgumentOutOfRangeException(Origin, ex, _localizer));
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiErrorFactory.FromArgumentException(Origin, ex, _localizer));
        }
    }

    /// <summary>
    /// Uploads and assigns a new symbol file to the security.
    /// </summary>
    /// <param name="id">Security identifier.</param>
    /// <param name="file">Form file containing the uploaded symbol image or asset.</param>
    /// <param name="categoryId">Optional category id to classify the attachment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with the created <see cref="AttachmentDto"/>, or 400 Bad Request for invalid input, or 500 on unexpected errors.</returns>
    [HttpPost("{id:guid}/symbol")]
    [RequestSizeLimit(long.MaxValue)]
    [ProducesResponseType(typeof(AttachmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadSymbolAsync(Guid id, [FromForm] IFormFile? file, [FromForm] Guid? categoryId, CancellationToken ct)
    {
        if (file == null)
        {
            var err = ApiErrorDto.Create(Origin, "Err_Invalid_File", _localizer[$"{Origin}_Err_Invalid_File"].ResourceNotFound
                ? "File required"
                : _localizer[$"{Origin}_Err_Invalid_File"].Value);

            return BadRequest(err);
        }

        try
        {
            using var stream = file.OpenReadStream();
            var dto = await _attachments.UploadAsync(_current.UserId, AttachmentEntityKind.Security, id, stream, file.FileName, file.ContentType ?? "application/octet-stream", categoryId, AttachmentRole.Symbol, ct);
            await _service.SetSymbolAttachmentAsync(id, _current.UserId, dto.Id, ct);
            return Ok(dto);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ApiErrorFactory.FromArgumentOutOfRangeException(Origin, ex, _localizer));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiErrorFactory.FromArgumentException(Origin, ex, _localizer));
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
        }
    }

    private static int? NormalizeYears(int? maxYearsBack) { if (!maxYearsBack.HasValue) return null; return Math.Clamp(maxYearsBack.Value, 1, 10); }
    private static AggregatePeriod ParsePeriod(string period) { if (!Enum.TryParse<AggregatePeriod>(period, true, out var p)) { p = AggregatePeriod.Month; } return p; }
    private static int NormalizeTake(AggregatePeriod p, int take) { var def = p == AggregatePeriod.Month ? 36 : p == AggregatePeriod.Quarter ? 16 : p == AggregatePeriod.HalfYear ? 12 : 10; return Math.Clamp(take <= 0 ? def : take, 1, 200); }

    private async Task<ActionResult<IReadOnlyList<AggregatePointDto>>> GetAggregatesInternalAsync(Guid securityId, string period, int take, int? maxYearsBack, CancellationToken ct)
    {
        var p = ParsePeriod(period); take = NormalizeTake(p, take); var years = NormalizeYears(maxYearsBack);
        var data = await _series.GetAsync(_current.UserId, PostingKind.Security, securityId, p, take, years, ct);
        if (data == null) return NotFound();
        return Ok(data.Select(a => new AggregatePointDto(a.PeriodStart, a.Amount)).ToList());
    }

    /// <summary>
    /// Returns time series aggregates for a security.
    /// </summary>
    /// <param name="securityId">Security identifier.</param>
    /// <param name="period">Aggregate period name (e.g. "Month", "Quarter"). Case-insensitive.</param>
    /// <param name="take">Number of periods to take. Will be normalized to a safe range.</param>
    /// <param name="maxYearsBack">Optional maximum years back to include.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with a list of <see cref="AggregatePointDto"/>, or 404 NotFound when no data is available.</returns>
    [HttpGet("{securityId:guid}/aggregates")]
    [ProducesResponseType(typeof(IReadOnlyList<AggregatePointDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<IReadOnlyList<AggregatePointDto>>> GetAggregatesAsync(Guid securityId, [FromQuery] string period = "Month", [FromQuery] int take = 36, [FromQuery] int? maxYearsBack = null, CancellationToken ct = default)
        => GetAggregatesInternalAsync(securityId, period, take, maxYearsBack, ct);

    /// <summary>
    /// Returns historical prices (paged latest first) for a security.
    /// </summary>
    /// <param name="id">Security identifier.</param>
    /// <param name="skip">Number of records to skip (paging offset).</param>
    /// <param name="take">Number of records to take (page size). Clamped to a maximum for safety.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with a list of <see cref="SecurityPriceDto"/>, or 404 NotFound when the security is not owned by the current user.</returns>
    [HttpGet("{id:guid}/prices")]
    [ProducesResponseType(typeof(IReadOnlyList<SecurityPriceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SecurityPriceDto>>> GetPricesAsync(Guid id, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        const int MaxTake = 250;
        take = Math.Clamp(take, 1, MaxTake);
        var list = await _priceService.ListAsync(_current.UserId, id, skip, take, ct);
        return Ok(list);
    }

    /// <summary>
    /// Enqueues a background task for backfilling security prices.
    /// </summary>
    /// <param name="req">Backfill request containing security id and optional date range.</param>
    /// <returns>200 OK with <see cref="BackgroundTaskInfo"/> describing the enqueued task.</returns>
    [HttpPost("backfill")]
    [ProducesResponseType(typeof(BackgroundTaskInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<BackgroundTaskInfo> EnqueueBackfill([FromBody] SecurityBackfillRequest req)
    {
        var payload = new { SecurityId = req.SecurityId?.ToString(), FromDateUtc = req.FromDateUtc?.ToString("o"), ToDateUtc = req.ToDateUtc?.ToString("o") };
        var info = _tasks.Enqueue(BackgroundTaskType.SecurityPricesBackfill, _current.UserId, payload, allowDuplicate: false);
        return Ok(info);
    }

    /// <summary>
    /// Returns quarterly dividend aggregates for the past year across all owned securities.
    /// </summary>
    /// <param name="period">Optional period parameter (ignored; kept for compatibility).</param>
    /// <param name="take">Optional take parameter (ignored; kept for compatibility).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with a list of quarterly <see cref="AggregatePointDto"/> objects.</returns>
    [HttpGet("dividends")]
    [ProducesResponseType(typeof(IReadOnlyList<AggregatePointDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AggregatePointDto>>> GetDividendsAsync([FromQuery] string? period = null, [FromQuery] int? take = null, CancellationToken ct = default)
    {
        var data = await _reports.GetDividendAggregatesAsync(_current.UserId, ct);
        return Ok(data);
    }
}

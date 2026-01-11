using FinanceManager.Application;
using FinanceManager.Application.Budget;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace FinanceManager.Web.Controllers;

/// <summary>
/// Manages budget purposes for the current user.
/// </summary>
[ApiController]
[Route("api/budget/purposes")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class BudgetPurposesController : ControllerBase
{
    private readonly IBudgetPurposeService _svc;
    private readonly ICurrentUserService _current;
    private readonly ILogger<BudgetPurposesController> _logger;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public BudgetPurposesController(IBudgetPurposeService svc, ICurrentUserService current, ILogger<BudgetPurposesController> logger)
    {
        _svc = svc;
        _current = current;
        _logger = logger;
    }

    /// <summary>
    /// Lists purposes for the current user.
    /// When <paramref name="from"/> and <paramref name="to"/> are provided, returns an overview including rule count and budget sum.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BudgetPurposeOverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 200,
        [FromQuery] BudgetSourceType? sourceType = null,
        [FromQuery] string? q = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
    {
        try
        {
            var list = await _svc.ListOverviewAsync(_current.UserId, skip, take, sourceType, q, from, to, ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "List budget purposes failed");
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Gets a budget purpose by id.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BudgetPurposeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.GetAsync(id, _current.UserId, ct);
            return dto == null ? NotFound() : Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get budget purpose failed {PurposeId}", id);
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Creates a budget purpose.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BudgetPurposeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync([FromBody] BudgetPurposeCreateRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var created = await _svc.CreateAsync(_current.UserId, req.Name, req.SourceType, req.SourceId, req.Description, ct);
            return Created($"/api/budget/purposes/{created.Id}", created);
        }
        catch (AggregateException ex)
        {
            foreach (var inner in ex.InnerExceptions)
            {
                ModelState.AddModelError(string.Empty, inner.Message);
            }
            return ValidationProblem(ModelState);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiErrorDto(nameof(ArgumentException), ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create budget purpose failed");
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Updates a budget purpose.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] BudgetPurposeUpdateRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var updated = await _svc.UpdateAsync(id, _current.UserId, req.Name, req.SourceType, req.SourceId, req.Description, ct);
            return updated == null ? NotFound() : NoContent();
        }
        catch (AggregateException ex)
        {
            foreach (var inner in ex.InnerExceptions)
            {
                ModelState.AddModelError(string.Empty, inner.Message);
            }
            return ValidationProblem(ModelState);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiErrorDto(nameof(ArgumentException), ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update budget purpose failed {PurposeId}", id);
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Deletes a budget purpose.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var ok = await _svc.DeleteAsync(id, _current.UserId, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete budget purpose failed {PurposeId}", id);
            return Problem("Unexpected error", statusCode: 500);
        }
    }
}

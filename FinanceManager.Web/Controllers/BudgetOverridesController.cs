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
/// Manages budget overrides for the current user.
/// </summary>
[ApiController]
[Route("api/budget/overrides")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class BudgetOverridesController : ControllerBase
{
    private readonly IBudgetOverrideService _svc;
    private readonly ICurrentUserService _current;
    private readonly ILogger<BudgetOverridesController> _logger;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public BudgetOverridesController(IBudgetOverrideService svc, ICurrentUserService current, ILogger<BudgetOverridesController> logger)
    {
        _svc = svc;
        _current = current;
        _logger = logger;
    }

    /// <summary>
    /// Lists overrides for a specific purpose.
    /// </summary>
    [HttpGet("by-purpose/{budgetPurposeId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<BudgetOverrideDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByPurposeAsync(Guid budgetPurposeId, CancellationToken ct)
    {
        try
        {
            var list = await _svc.ListByPurposeAsync(_current.UserId, budgetPurposeId, ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "List budget overrides failed {BudgetPurposeId}", budgetPurposeId);
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Gets an override by id.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BudgetOverrideDto), StatusCodes.Status200OK)]
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
            _logger.LogError(ex, "Get budget override failed {OverrideId}", id);
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Creates an override.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BudgetOverrideDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync([FromBody] BudgetOverrideCreateRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var created = await _svc.CreateAsync(_current.UserId, req.BudgetPurposeId, req.Period, req.Amount, ct);
            return Created($"/api/budget/overrides/{created.Id}", created);
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
            _logger.LogError(ex, "Create budget override failed");
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Updates an existing override.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] BudgetOverrideUpdateRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var updated = await _svc.UpdateAsync(id, _current.UserId, req.Period, req.Amount, ct);
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
            _logger.LogError(ex, "Update budget override failed {OverrideId}", id);
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Deletes an override.
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
            _logger.LogError(ex, "Delete budget override failed {OverrideId}", id);
            return Problem("Unexpected error", statusCode: 500);
        }
    }
}

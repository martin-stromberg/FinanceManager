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
/// Manages budget rules for the current user.
/// </summary>
[ApiController]
[Route("api/budget/rules")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class BudgetRulesController : ControllerBase
{
    private readonly IBudgetRuleService _svc;
    private readonly ICurrentUserService _current;
    private readonly ILogger<BudgetRulesController> _logger;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public BudgetRulesController(IBudgetRuleService svc, ICurrentUserService current, ILogger<BudgetRulesController> logger)
    {
        _svc = svc;
        _current = current;
        _logger = logger;
    }

    /// <summary>
    /// Lists rules for a specific purpose.
    /// </summary>
    [HttpGet("by-purpose/{budgetPurposeId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<BudgetRuleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByPurposeAsync(Guid budgetPurposeId, CancellationToken ct)
    {
        try
        {
            var list = await _svc.ListByPurposeAsync(_current.UserId, budgetPurposeId, ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "List budget rules failed {BudgetPurposeId}", budgetPurposeId);
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Gets a rule by id.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BudgetRuleDto), StatusCodes.Status200OK)]
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
            _logger.LogError(ex, "Get budget rule failed {RuleId}", id);
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Creates a budget rule.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BudgetRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync([FromBody] BudgetRuleCreateRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var created = await _svc.CreateAsync(_current.UserId, req.BudgetPurposeId, req.Amount, req.Interval, req.CustomIntervalMonths, req.StartDate, req.EndDate, ct);
            return Created($"/api/budget/rules/{created.Id}", created);
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
            _logger.LogError(ex, "Create budget rule failed");
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Updates an existing budget rule.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] BudgetRuleUpdateRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var updated = await _svc.UpdateAsync(id, _current.UserId, req.Amount, req.Interval, req.CustomIntervalMonths, req.StartDate, req.EndDate, ct);
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
            _logger.LogError(ex, "Update budget rule failed {RuleId}", id);
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Deletes a budget rule.
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
            _logger.LogError(ex, "Delete budget rule failed {RuleId}", id);
            return Problem("Unexpected error", statusCode: 500);
        }
    }
}

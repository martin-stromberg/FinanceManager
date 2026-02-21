using FinanceManager.Application;
using FinanceManager.Application.Budget;
using FinanceManager.Application.Common;
using FinanceManager.Application.Exceptions;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.Common;
using FinanceManager.Web.Infrastructure.ApiErrors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
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
    private const string Origin = "API_BudgetRule";

    private readonly IBudgetRuleService _svc;
    private readonly ICurrentUserService _current;
    private readonly ILogger<BudgetRulesController> _logger;
    private readonly IStringLocalizer<Controller> _localizer;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public BudgetRulesController(
        IBudgetRuleService svc,
        ICurrentUserService current,
        ILogger<BudgetRulesController> logger,
        IStringLocalizer<Controller> localizer)
    {
        _svc = svc;
        _current = current;
        _logger = logger;
        _localizer = localizer;
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
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
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
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
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
            BudgetRuleDto created;

            var hasPurpose = req.BudgetPurposeId.HasValue && req.BudgetPurposeId.Value != Guid.Empty;
            var hasCategory = req.BudgetCategoryId.HasValue && req.BudgetCategoryId.Value != Guid.Empty;

            if (hasPurpose == hasCategory)
            {
                var ex = new ArgumentException("Exactly one of BudgetPurposeId or BudgetCategoryId must be provided", nameof(BudgetRuleCreateRequest.BudgetPurposeId));
                return BadRequest(ApiErrorFactory.FromArgumentException(Origin, ex, _localizer));
            }

            if (hasPurpose)
            {
                created = await _svc.CreateAsync(_current.UserId, req.BudgetPurposeId!.Value, req.Amount, req.Interval, req.CustomIntervalMonths, req.StartDate, req.EndDate, ct);
            }
            else
            {
                created = await _svc.CreateForCategoryAsync(_current.UserId, req.BudgetCategoryId!.Value, req.Amount, req.Interval, req.CustomIntervalMonths, req.StartDate, req.EndDate, ct);
            }

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
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ApiErrorFactory.FromArgumentOutOfRangeException(Origin, ex, _localizer));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiErrorFactory.FromArgumentException(Origin, ex, _localizer));
        }
        catch (DomainValidationException ex)
        {
            return Conflict(ApiErrorFactory.FromDomainValidationException(Origin, ex, _localizer));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create budget rule failed");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
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
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ApiErrorFactory.FromArgumentOutOfRangeException(Origin, ex, _localizer));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiErrorFactory.FromArgumentException(Origin, ex, _localizer));
        }
        catch (DomainValidationException ex)
        {
            return Conflict(ApiErrorFactory.FromDomainValidationException(Origin, ex, _localizer));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update budget rule failed {RuleId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
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
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
        }
    }

    /// <summary>
    /// Lists rules for a budget category.
    /// </summary>
    [HttpGet("by-category/{budgetCategoryId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<BudgetRuleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByCategoryAsync(Guid budgetCategoryId, CancellationToken ct)
    {
        try
        {
            var list = await _svc.ListByCategoryAsync(_current.UserId, budgetCategoryId, ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "List budget rules by category failed {CategoryId}", budgetCategoryId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
        }
    }
}

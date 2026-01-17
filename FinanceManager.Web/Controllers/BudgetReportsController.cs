using FinanceManager.Application;
using FinanceManager.Application.Budget;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Web.Infrastructure.ApiErrors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Net.Mime;

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

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public BudgetReportsController(
        IBudgetReportService reports,
        ICurrentUserService current,
        ILogger<BudgetReportsController> logger,
        IStringLocalizer<Controller> localizer)
    {
        _reports = reports;
        _current = current;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Generates a budget report for the current user.
    /// </summary>
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

            var dto = await _reports.GetAsync(_current.UserId, req, ct);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get budget report failed");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
        }
    }
}

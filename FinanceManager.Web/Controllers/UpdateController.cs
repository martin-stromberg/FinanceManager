#pragma warning disable CS1591
using FinanceManager.Shared.Dtos.Common;
using FinanceManager.Shared.Dtos.Update;
using FinanceManager.Web.Services.Updates;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace FinanceManager.Web.Controllers;

[ApiController]
[Route("api/setup/update")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public sealed class UpdateController : ControllerBase
{
    private const string Origin = "API_Update";
    private readonly IUpdateOrchestrator _orchestrator;
    private readonly ILogger<UpdateController> _logger;

    public UpdateController(IUpdateOrchestrator orchestrator, ILogger<UpdateController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(UpdateStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(CancellationToken ct)
        => Ok(await _orchestrator.GetStatusAsync(ct));

    [HttpGet("settings")]
    [ProducesResponseType(typeof(UpdateSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Settings(CancellationToken ct)
        => Ok(await _orchestrator.GetSettingsAsync(ct));

    [HttpPut("settings")]
    [ProducesResponseType(typeof(UpdateSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsUpdateRequest request, CancellationToken ct)
        => Ok(await _orchestrator.SaveSettingsAsync(request, ct));

    [HttpPost("check")]
    [ProducesResponseType(typeof(UpdateCheckResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Check(CancellationToken ct)
        => Ok(await _orchestrator.CheckAsync(ct));

    [HttpPost("schedule")]
    [ProducesResponseType(typeof(UpdateSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Schedule([FromBody] UpdateScheduleRequest request, CancellationToken ct)
        => Ok(await _orchestrator.ScheduleAsync(request.ScheduledInstallTime, ct));

    [HttpPost("install/start")]
    [ProducesResponseType(typeof(UpdateStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartInstall([FromBody] UpdateStartRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _orchestrator.StartInstallAsync(request.ConfirmDowntime, ct));
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(ApiErrorDto.Create(Origin, "Err_Update_NotReady", ex.Message));
        }
        catch (IOException ex)
        {
            return Conflict(ApiErrorDto.Create(Origin, "Err_Update_Locked", ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiErrorDto.Create(Origin, "Err_Update_InvalidRequest", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiErrorDto.Create(Origin, "Err_Update_InvalidState", ex.Message));
        }
    }

    [HttpPost("lock/reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResetLock([FromBody] UpdateLockResetRequest request, CancellationToken ct)
    {
        try
        {
            _logger.LogWarning("Update lock reset requested by {User}. Reason: {Reason}", User.Identity?.Name, request.Reason);
            await _orchestrator.ResetLockAsync(request.Reason, ct);
            return NoContent();
        }
        catch (IOException ex)
        {
            return Conflict(ApiErrorDto.Create(Origin, "Err_Update_InstallRunning", ex.Message));
        }
    }
}
#pragma warning restore CS1591

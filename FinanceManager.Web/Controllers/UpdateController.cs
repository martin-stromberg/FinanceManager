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
    private readonly IUpdateServiceCatalog _serviceCatalog;
    private readonly ILogger<UpdateController> _logger;

    public UpdateController(IUpdateOrchestrator orchestrator, IUpdateServiceCatalog serviceCatalog, ILogger<UpdateController> logger)
    {
        _orchestrator = orchestrator;
        _serviceCatalog = serviceCatalog;
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

    [HttpGet("services")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Services([FromQuery] string? query, [FromQuery] int take = 20, CancellationToken ct = default)
        => Ok(await _serviceCatalog.ListServiceNamesAsync(query, take, ct));

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
            _logger.LogInformation("Update installation requested by {User}. ConfirmDowntime: {ConfirmDowntime}", User.Identity?.Name, request.ConfirmDowntime);
            return Ok(await _orchestrator.StartInstallAsync(request.ConfirmDowntime, ct));
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Update installation failed: {Message}", ex.Message);
            return NotFound(ApiErrorDto.Create(Origin, "Err_Update_NotReady", ex.Message));
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Update installation failed: {Message}", ex.Message);
            return Conflict(ApiErrorDto.Create(Origin, "Err_Update_Locked", ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Update installation failed: {Message}", ex.Message);
            return BadRequest(ApiErrorDto.Create(Origin, "Err_Update_InvalidRequest", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Update installation failed: {Message}", ex.Message);
            return BadRequest(ApiErrorDto.Create(Origin, "Err_Update_InvalidState", ex.Message));
        }
    }

    [HttpPost("lock/reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResetLock([FromBody] UpdateLockResetRequest request, CancellationToken ct)
    {
        try
        {
            var sanitizedReason = request.Reason?
                .Replace("\r", " ")
                .Replace("\n", " ");

            _logger.LogWarning("Update lock reset requested by {User}. Reason: {Reason}", User.Identity?.Name, sanitizedReason);
            await _orchestrator.ResetLockAsync(request.Reason, ct);
            return NoContent();
        }
        catch (UpdateLockResetException ex)
        {
            var statusCode = MapResetFailureStatusCode(ex.Kind);
            var errorCode = MapResetFailureCode(ex.Kind);
            LogResetFailure(ex);
            return StatusCode(statusCode, ApiErrorDto.Create(Origin, errorCode, ex.Message));
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Update lock reset failed with an unclassified I/O error: {Message}", ex.Message);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiErrorDto.Create(Origin, "Err_Update_Reset_Failed", ex.Message));
        }
    }

    private static int MapResetFailureStatusCode(UpdateLockResetFailureKind kind)
        => kind == UpdateLockResetFailureKind.ResetFailed
            ? StatusCodes.Status500InternalServerError
            : StatusCodes.Status409Conflict;

    private static string MapResetFailureCode(UpdateLockResetFailureKind kind)
        => kind switch
        {
            UpdateLockResetFailureKind.NoLock => "Err_Update_Reset_NoLock",
            UpdateLockResetFailureKind.LockNotStale => "Err_Update_Reset_LockNotStale",
            UpdateLockResetFailureKind.LockDeleteFailed => "Err_Update_Reset_DeleteFailed",
            _ => "Err_Update_Reset_Failed"
        };

    private void LogResetFailure(UpdateLockResetException ex)
    {
        var logLevel = ex.Kind == UpdateLockResetFailureKind.ResetFailed ? LogLevel.Error : LogLevel.Warning;
        _logger.Log(
            logLevel,
            ex,
            "Update lock reset failed. Kind: {Kind}; Source: {FailureSource}; LockCreatedAt: {LockCreatedAt}; LockPath: {LockPath}; User: {User}; Message: {Message}",
            ex.Kind,
            ex.FailureSource,
            ex.LockCreatedAt,
            ex.LockPath,
            User.Identity?.Name,
            ex.Message);
    }
}
#pragma warning restore CS1591

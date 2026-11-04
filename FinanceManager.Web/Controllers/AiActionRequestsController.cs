using FinanceManager.Application;
using FinanceManager.Application.Exceptions;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Common;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Web.Infrastructure.ApiErrors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Net.Mime;

namespace FinanceManager.Web.Controllers;

/// <summary>
/// Approval API of the MCP action requests (F-0905, approval-konzept.md §4/§7): inbox list
/// with status/access filters, detail incl. the frozen parameter snapshot and the decision
/// transitions <c>approve</c>/<c>reject</c>/<c>cancel</c>. All operations are strictly scoped
/// to <see cref="ICurrentUserService.UserId"/> — a foreign request id behaves like a missing
/// one (404 semantics). The <c>IConfirmationService</c> UX of critical approvals is a
/// presentation concern and ships with the approval center UI (EP-10).
/// </summary>
[ApiController]
[Route("api/mcp-approvals")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class AiActionRequestsController : ControllerBase
{
    private const string Origin = "API_McpApproval";

    private readonly IMcpApprovalService _svc;
    private readonly ICurrentUserService _current;
    private readonly ILogger<AiActionRequestsController> _logger;
    private readonly IStringLocalizer<Controller> _localizer;

    /// <summary>Creates the controller.</summary>
    public AiActionRequestsController(
        IMcpApprovalService svc,
        ICurrentUserService current,
        ILogger<AiActionRequestsController> logger,
        IStringLocalizer<Controller> localizer)
    {
        _svc = svc;
        _current = current;
        _logger = logger;
        _localizer = localizer;
    }

    private IActionResult Error(Exception ex)
        => ex switch
        {
            DomainValidationException dv => BadRequest(ApiErrorFactory.FromDomainValidationException(Origin, dv, _localizer)),
            ArgumentException ae => BadRequest(ApiErrorFactory.FromArgumentException(Origin, ae, _localizer)),
            _ => LogAnd500(ex)
        };

    private IActionResult LogAnd500(Exception ex)
    {
        _logger.LogError(ex, "MCP approval operation failed");
        return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
    }

    private IActionResult BadStatus()
        => BadRequest(ApiErrorDto.Create(Origin, "Err_Mcp_InvalidArguments",
            _localizer[$"{Origin}_Err_Mcp_InvalidArguments"].ResourceNotFound
                ? "The status filter is not a valid request state."
                : _localizer[$"{Origin}_Err_Mcp_InvalidArguments"].Value));

    /// <summary>Lists the own approval requests (inbox and history), newest first.</summary>
    /// <param name="status">Optional status filter (AiActionRequestStatus as short).</param>
    /// <param name="accessId">Optional requesting-access filter.</param>
    /// <param name="skip">Items to skip.</param>
    /// <param name="take">Items to take (clamped ≤ 200).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AiActionRequestListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] short? status, [FromQuery] Guid? accessId,
        [FromQuery] int skip = 0, [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        if (status.HasValue && !Enum.IsDefined(typeof(AiActionRequestStatus), (int)status.Value))
        {
            return BadStatus();
        }
        try
        {
            return Ok(await _svc.ListAsync(_current.UserId,
                status.HasValue ? (AiActionRequestStatus)status.Value : null,
                accessId, skip, take, ct));
        }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>Gets one request detail (404 when not found/not owned). The detail carries the
    /// frozen parameter snapshot, the client reason text and the decision metadata — the
    /// server-side basis of the approval-center effect preview (EP-10).</summary>
    /// <param name="id">Request id.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AiActionRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.GetAsync(_current.UserId, id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>Approves a pending request (<c>Pending → Approved</c>) — it enters the
    /// persistent execution queue and runs exactly once via the regular service path.</summary>
    /// <param name="id">Request id.</param>
    /// <param name="req">Optional decision comment.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveAsync(Guid id, [FromBody] AiActionRequestDecisionRequest? req, CancellationToken ct)
        => await DecideAsync(id, req?.DecisionComment, ct, approve: true);

    /// <summary>Rejects a pending request (<c>Pending → Rejected</c>); the comment is played
    /// back to the client as decision feedback.</summary>
    /// <param name="id">Request id.</param>
    /// <param name="req">Optional rejection reason.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectAsync(Guid id, [FromBody] AiActionRequestDecisionRequest? req, CancellationToken ct)
        => await DecideAsync(id, req?.DecisionComment, ct, approve: false);

    /// <summary>Withdraws a still-pending request of the owner (owner-side cancel; the
    /// request ends <c>Rejected</c> with a withdrawal note).</summary>
    /// <param name="id">Request id.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAsync(Guid id, CancellationToken ct)
    {
        try
        {
            return await _svc.CancelAsync(_current.UserId, id, ct) ? NoContent() : NotFound();
        }
        catch (Exception ex) { return Error(ex); }
    }

    private async Task<IActionResult> DecideAsync(Guid id, string? comment, CancellationToken ct, bool approve)
    {
        try
        {
            var ok = approve
                ? await _svc.ApproveAsync(_current.UserId, id, comment, ct)
                : await _svc.RejectAsync(_current.UserId, id, comment, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) { return Error(ex); }
    }
}

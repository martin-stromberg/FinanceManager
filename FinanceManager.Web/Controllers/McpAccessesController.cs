using FinanceManager.Application;
using FinanceManager.Application.Exceptions;
using FinanceManager.Application.Mcp;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Web.Infrastructure.ApiErrors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Net.Mime;

namespace FinanceManager.Web.Controllers;

/// <summary>
/// Administration API of the MCP accesses (F-0104): CRUD, token rotation, status switches,
/// scope/filter management and server-side level/options metadata. All operations are
/// strictly scoped to <see cref="ICurrentUserService.UserId"/>; plaintext tokens appear
/// exactly once in create/rotate responses.
/// </summary>
[ApiController]
[Route("api/mcp-accesses")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class McpAccessesController : ControllerBase
{
    private const string Origin = "API_McpAccess";

    private readonly IMcpAccessService _svc;
    private readonly ICurrentUserService _current;
    private readonly ILogger<McpAccessesController> _logger;
    private readonly IStringLocalizer<Controller> _localizer;

    /// <summary>Creates the controller.</summary>
    public McpAccessesController(
        IMcpAccessService svc,
        ICurrentUserService current,
        ILogger<McpAccessesController> logger,
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
        _logger.LogError(ex, "MCP access operation failed");
        return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
    }

    /// <summary>Lists own accesses (search + paging).</summary>
    /// <param name="q">Optional name search text.</param>
    /// <param name="skip">Items to skip.</param>
    /// <param name="take">Items to take (clamped ≤ 200).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<McpAccessListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] string? q, [FromQuery] int skip = 0, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        try { return Ok(await _svc.ListAsync(_current.UserId, q, skip, take, ct)); }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>Gets one access detail (404 when not found/not owned).</summary>
    /// <param name="id">Access id.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(McpAccessDetailDto), StatusCodes.Status200OK)]
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

    /// <summary>Creates an access; response contains the plaintext token exactly once.</summary>
    /// <param name="req">Creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [ProducesResponseType(typeof(McpAccessCreatedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync([FromBody] McpAccessCreateRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        try
        {
            var created = await _svc.CreateAsync(_current.UserId, req, ct);
            return Created($"/api/mcp-accesses/{created.Access.Id}", created);
        }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>Updates master data, level, approval mode, expiry and IP binding.</summary>
    /// <param name="id">Access id.</param>
    /// <param name="req">Update request.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] McpAccessUpdateRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        try
        {
            return await _svc.UpdateAsync(_current.UserId, id, req, ct) ? NoContent() : NotFound();
        }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>Replaces the persisted scope profile (individual level only).</summary>
    /// <param name="id">Access id.</param>
    /// <param name="req">Scope set request.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{id:guid}/scopes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetScopesAsync(Guid id, [FromBody] McpScopeSetRequest req, CancellationToken ct)
    {
        try
        {
            return await _svc.SetScopesAsync(_current.UserId, id, req, ct) ? NoContent() : NotFound();
        }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>Replaces the filter dimensions of one persisted scope row.</summary>
    /// <param name="id">Access id.</param>
    /// <param name="scopeId">Scope row id.</param>
    /// <param name="req">Filter set request.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{id:guid}/filters/{scopeId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetFiltersAsync(Guid id, Guid scopeId, [FromBody] McpFilterSetRequest req, CancellationToken ct)
    {
        try
        {
            return await _svc.SetFiltersAsync(_current.UserId, id, scopeId, req, ct) ? NoContent() : NotFound();
        }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>Activates an access (effective immediately).</summary>
    /// <param name="id">Access id.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateAsync(Guid id, CancellationToken ct)
        => await StatusChange(id, ct, activate: true);

    /// <summary>Deactivates an access (effective immediately).</summary>
    /// <param name="id">Access id.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateAsync(Guid id, CancellationToken ct)
        => await StatusChange(id, ct, activate: false);

    private async Task<IActionResult> StatusChange(Guid id, CancellationToken ct, bool activate)
    {
        try
        {
            var ok = activate
                ? await _svc.ActivateAsync(_current.UserId, id, ct)
                : await _svc.DeactivateAsync(_current.UserId, id, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>Rotates the token; response contains the new plaintext exactly once.</summary>
    /// <param name="id">Access id.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/rotate-token")]
    [ProducesResponseType(typeof(McpAccessRotatedDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RotateTokenAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await _svc.RotateTokenAsync(_current.UserId, id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>Revokes (deletes) an access; revocation is effective immediately.</summary>
    /// <param name="id">Access id.</param>
    /// <param name="req">Optional revocation reason.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeAsync(Guid id, [FromBody] McpAccessRevokeRequest? req, CancellationToken ct)
    {
        try
        {
            return await _svc.RevokeAsync(_current.UserId, id, req?.Reason, ct) ? NoContent() : NotFound();
        }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>Lists the server-side level definitions incl. default scope profiles.</summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("levels")]
    [ProducesResponseType(typeof(IReadOnlyList<McpAccessLevelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLevelsAsync(CancellationToken ct)
    {
        try { return Ok(await _svc.GetLevelsAsync(ct)); }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>UI options: scope matrix, filter dimensions, category lists, TLS flag.</summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("options")]
    [ProducesResponseType(typeof(McpAccessOptionsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOptionsAsync(CancellationToken ct)
    {
        try { return Ok(await _svc.GetOptionsAsync(_current.UserId, Request.IsHttps, ct)); }
        catch (Exception ex) { return Error(ex); }
    }
}

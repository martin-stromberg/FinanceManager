using FinanceManager.Application;
using FinanceManager.Application.Exceptions;
using FinanceManager.Application.Mcp;
using FinanceManager.Infrastructure.Mcp;
using FinanceManager.Shared.Dtos.Common;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Web.Infrastructure.ApiErrors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

namespace FinanceManager.Web.Controllers;

/// <summary>
/// Read API of the MCP audit trail (F-0601, audit-konzept.md §6, ui-konzept.md §6.3):
/// filtered timeline list, entry detail with field deltas, hash-chain verification and
/// the csv/json export of the own protocol. All operations are strictly scoped to
/// <see cref="ICurrentUserService.UserId"/>; the audit trail is append-only — this
/// controller exposes no mutation path.
/// </summary>
[ApiController]
[Route("api/mcp-audit")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class McpAuditController : ControllerBase
{
    private const string Origin = "API_McpAudit";

    /// <summary>Server-side row cap of one export request.</summary>
    private const int ExportMaxRows = 5000;

    private readonly IMcpAuditQueryService _svc;
    private readonly ICurrentUserService _current;
    private readonly ILogger<McpAuditController> _logger;
    private readonly IStringLocalizer<Controller> _localizer;

    /// <summary>Creates the controller.</summary>
    public McpAuditController(
        IMcpAuditQueryService svc,
        ICurrentUserService current,
        ILogger<McpAuditController> logger,
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
        _logger.LogError(ex, "MCP audit operation failed");
        return StatusCode(StatusCodes.Status500InternalServerError, ApiErrorFactory.Unexpected(Origin, _localizer));
    }

    private IActionResult BadFormat()
        => BadRequest(ApiErrorDto.Create(Origin, "Err_Invalid_format",
            _localizer[$"{Origin}_Err_Invalid_format"].ResourceNotFound
                ? "Invalid export format. Supported formats are 'csv' and 'json'."
                : _localizer[$"{Origin}_Err_Invalid_format"].Value));

    /// <summary>Lists the own audit entries (filters + paging, newest first).</summary>
    /// <param name="from">Optional inclusive lower bound (UTC).</param>
    /// <param name="to">Optional inclusive upper bound (UTC).</param>
    /// <param name="category">Optional category (McpAuditCategory as short).</param>
    /// <param name="mcpAccessId">Optional access restriction.</param>
    /// <param name="correlationId">Optional correlation id.</param>
    /// <param name="toolName">Optional exact <c>fm_*</c> tool name.</param>
    /// <param name="result">Optional exact result code.</param>
    /// <param name="skip">Items to skip.</param>
    /// <param name="take">Items to take (clamped ≤ 200).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<McpAuditEntryListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] short? category, [FromQuery] Guid? mcpAccessId,
        [FromQuery] Guid? correlationId, [FromQuery] string? toolName,
        [FromQuery] string? result,
        [FromQuery] int skip = 0, [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        try
        {
            var filter = new McpAuditQueryFilter(from, to, category, mcpAccessId, correlationId, toolName, result);
            return Ok(await _svc.ListAsync(_current.UserId, filter, skip, take, ct));
        }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>Gets one audit entry detail incl. payload and field deltas (404 when not found/not owned).</summary>
    /// <param name="id">Entry id.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(McpAuditEntryDetailDto), StatusCodes.Status200OK)]
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

    /// <summary>
    /// Verifies the integrity of the own audit hash chain (audit-konzept.md §7.2/§7.3):
    /// monotonic gap-free sequence, intact predecessor linkage and valid entry hashes.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("verify")]
    [ProducesResponseType(typeof(McpAuditVerifyResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyAsync(CancellationToken ct)
    {
        try { return Ok(await _svc.VerifyAsync(_current.UserId, ct)); }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>
    /// Exports the own audit protocol as JSON (full detail incl. field deltas) or CSV
    /// (compact row view). The export itself is recorded as an audit event (F-0601 AK).
    /// </summary>
    /// <param name="format"><c>json</c> or <c>csv</c>.</param>
    /// <param name="from">Optional inclusive lower bound (UTC).</param>
    /// <param name="to">Optional inclusive upper bound (UTC).</param>
    /// <param name="category">Optional category (McpAuditCategory as short).</param>
    /// <param name="mcpAccessId">Optional access restriction.</param>
    /// <param name="correlationId">Optional correlation id.</param>
    /// <param name="toolName">Optional exact <c>fm_*</c> tool name.</param>
    /// <param name="result">Optional exact result code.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportAsync(
        [FromQuery] string format,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] short? category, [FromQuery] Guid? mcpAccessId,
        [FromQuery] Guid? correlationId, [FromQuery] string? toolName,
        [FromQuery] string? result,
        CancellationToken ct = default)
    {
        var fmt = (format ?? string.Empty).Trim().ToLowerInvariant();
        if (fmt is not ("csv" or "json"))
        {
            return BadFormat();
        }
        try
        {
            var filter = new McpAuditQueryFilter(from, to, category, mcpAccessId, correlationId, toolName, result);
            var rows = await _svc.ExportAsync(_current.UserId, filter, ExportMaxRows, ct);
            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmm");
            if (fmt == "csv")
            {
                var csv = McpAuditCsvExport.Write(rows);
                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
                return File(bytes, "text/csv; charset=utf-8", $"mcp-audit_{stamp}.csv");
            }
            var json = JsonSerializer.SerializeToUtf8Bytes(rows, new JsonSerializerOptions { WriteIndented = true });
            return File(json, "application/json; charset=utf-8", $"mcp-audit_{stamp}.json");
        }
        catch (Exception ex) { return Error(ex); }
    }

    /// <summary>
    /// Usage overview of one own access aggregated from the audit trail (F-0603):
    /// call counters, failure rate, top tools and the most recent calls.
    /// </summary>
    /// <param name="accessId">Access id.</param>
    /// <param name="recent">Optional size of the recent-calls excerpt (clamped).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("usage/{accessId:guid}")]
    [ProducesResponseType(typeof(McpAccessUsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUsageAsync(Guid accessId, [FromQuery] int recent = 10, CancellationToken ct = default)
    {
        try
        {
            var dto = await _svc.GetAccessUsageAsync(_current.UserId, accessId, recent, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (Exception ex) { return Error(ex); }
    }
}

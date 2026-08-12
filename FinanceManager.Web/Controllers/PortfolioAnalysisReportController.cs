using System.Text.Json;
using FinanceManager.Application;
using FinanceManager.Application.Portfolio;
using FinanceManager.Domain.Portfolio;
using FinanceManager.Shared.Dtos.Portfolio;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Web.Controllers;

/// <summary>
/// REST endpoints for the portfolio analysis report: reading the (cached) report, and reading/persisting
/// the current user's KPI tile configuration. All endpoints are scoped to the authenticated user via
/// <see cref="ICurrentUserService.UserId"/>.
/// </summary>
[ApiController]
[Route("api/portfolio")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class PortfolioAnalysisReportController : ControllerBase
{
    private readonly IPortfolioAnalysisReportCacheService _cache;
    private readonly IPortfolioKpiConfigurationRepository _configRepository;
    private readonly ICurrentUserService _current;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortfolioAnalysisReportController"/> class.
    /// </summary>
    /// <param name="cache">Portfolio analysis report cache service.</param>
    /// <param name="configRepository">Repository for the user's KPI tile configuration.</param>
    /// <param name="current">Service that provides information about the currently authenticated user.</param>
    public PortfolioAnalysisReportController(
        IPortfolioAnalysisReportCacheService cache,
        IPortfolioKpiConfigurationRepository configRepository,
        ICurrentUserService current)
    {
        _cache = cache;
        _configRepository = configRepository;
        _current = current;
    }

    /// <summary>
    /// Returns the portfolio analysis report for the current user, using the monthly cache when valid.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with the <see cref="PortfolioAnalysisReportDto"/>.</returns>
    /// <response code="200">Returns the portfolio analysis report.</response>
    [HttpGet("analysis-report")]
    [ProducesResponseType(typeof(PortfolioAnalysisReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnalysisReportAsync(CancellationToken ct)
        => Ok(await _cache.GetPortfolioReportAsync(_current.UserId, ct));

    /// <summary>
    /// Returns the current user's KPI tile configuration, or a default configuration when none has been saved yet.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with the <see cref="PortfolioKpiConfigurationDto"/>.</returns>
    /// <response code="200">Returns the current KPI tile configuration (or defaults).</response>
    [HttpGet("kpi-configuration")]
    [ProducesResponseType(typeof(PortfolioKpiConfigurationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKpiConfigurationAsync(CancellationToken ct)
    {
        var entity = await _configRepository.GetAsync(_current.UserId, ct);
        return Ok(entity == null ? DefaultConfigurationDto() : ToDto(entity));
    }

    /// <summary>
    /// Saves the current user's KPI tile configuration and invalidates the portfolio analysis report cache
    /// so the next view-mode load recomputes with the new configuration applied.
    /// </summary>
    /// <param name="req">KPI configuration request payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK with the persisted <see cref="PortfolioKpiConfigurationDto"/>, or 400 Bad Request for invalid input.</returns>
    /// <response code="200">Configuration saved and cache invalidated.</response>
    /// <response code="400">Request payload failed validation.</response>
    [HttpPost("kpi-configuration")]
    [ProducesResponseType(typeof(PortfolioKpiConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveKpiConfigurationAsync([FromBody] PortfolioKpiConfigurationRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) { return ValidationProblem(ModelState); }

        var activeSet = new HashSet<PortfolioTileId>(req.ActiveTileIds);
        if (activeSet.Count == 0)
        {
            ModelState.AddModelError(nameof(req.ActiveTileIds), "At least one tile must be active.");
            return ValidationProblem(ModelState);
        }

        var orderSet = new HashSet<PortfolioTileId>(req.TileOrder);
        if (orderSet.Count != req.TileOrder.Count || !activeSet.IsSubsetOf(orderSet))
        {
            ModelState.AddModelError(nameof(req.TileOrder), "Tile order must contain all active tiles without duplicates.");
            return ValidationProblem(ModelState);
        }

        var activeJson = JsonSerializer.Serialize(req.ActiveTileIds);
        var orderJson = JsonSerializer.Serialize(req.TileOrder);

        var entity = await _configRepository.UpsertAsync(_current.UserId, activeJson, orderJson, ct);
        await _cache.InvalidateCacheAsync(_current.UserId, ct);

        return Ok(ToDto(entity));
    }

    /// <summary>
    /// Manually resets (invalidates) the portfolio analysis report cache for the current user.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    /// <response code="204">Cache invalidated (or no cache entry existed).</response>
    [HttpPost("cache/reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetCacheAsync(CancellationToken ct)
    {
        await _cache.InvalidateCacheAsync(_current.UserId, ct);
        return NoContent();
    }

    private static PortfolioKpiConfigurationDto DefaultConfigurationDto()
    {
        var defaultTiles = new[] { PortfolioTileId.Structure, PortfolioTileId.Performance, PortfolioTileId.Cashflow };
        return new PortfolioKpiConfigurationDto(defaultTiles, defaultTiles, DateTime.UtcNow);
    }

    private static PortfolioKpiConfigurationDto ToDto(PortfolioKpiConfiguration entity)
    {
        var active = JsonSerializer.Deserialize<List<PortfolioTileId>>(entity.ActiveTileIds) ?? [];
        var order = JsonSerializer.Deserialize<List<PortfolioTileId>>(entity.TileOrder) ?? [];

        return new PortfolioKpiConfigurationDto(active, order, entity.UpdatedUtc);
    }
}

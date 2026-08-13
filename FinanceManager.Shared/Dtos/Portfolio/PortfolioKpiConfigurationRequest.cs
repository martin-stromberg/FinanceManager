using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Shared.Dtos.Portfolio;

/// <summary>
/// Request payload to persist the current user's portfolio KPI configuration.
/// </summary>
public sealed class PortfolioKpiConfigurationRequest
{
    /// <summary>Tiles that should be visible on the report page. Must contain at least one tile.</summary>
    [Required, MinLength(1)]
    public List<PortfolioTileId> ActiveTileIds { get; set; } = [];

    /// <summary>Tile ids in the desired display order. Must contain all active tile ids without duplicates.</summary>
    [Required, MinLength(1)]
    public List<PortfolioTileId> TileOrder { get; set; } = [];
}

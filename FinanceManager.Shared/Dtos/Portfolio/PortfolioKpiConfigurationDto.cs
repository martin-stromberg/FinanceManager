namespace FinanceManager.Shared.Dtos.Portfolio;

/// <summary>
/// Describes the current user's tile selection and tile order for the portfolio analysis report.
/// </summary>
/// <param name="ActiveTileIds">Tiles currently visible on the report page, in no particular order.</param>
/// <param name="TileOrder">Tile ids in display order. Contains the same set of ids as <paramref name="ActiveTileIds"/> plus any inactive tiles.</param>
/// <param name="UpdatedUtc">UTC timestamp of the last update.</param>
/// <returns>A portfolio KPI configuration record.</returns>
public sealed record PortfolioKpiConfigurationDto(
    IReadOnlyList<PortfolioTileId> ActiveTileIds,
    IReadOnlyList<PortfolioTileId> TileOrder,
    DateTime UpdatedUtc);

namespace FinanceManager.Shared.Dtos.Portfolio;

/// <summary>
/// A single slice of a distribution breakdown (asset allocation, region or sector).
/// </summary>
/// <param name="Label">Display label of the slice (category/region/sector name).</param>
/// <param name="Value">Market value of the slice in the user's reporting currency.</param>
/// <param name="Percentage">Share of the slice relative to the total market value (0..1).</param>
/// <returns>An allocation slice record.</returns>
public sealed record PortfolioAllocationSlice(string Label, decimal Value, decimal Percentage);

/// <summary>
/// A single position within the top-10-by-market-value list.
/// </summary>
/// <param name="SecurityId">Identifier of the security.</param>
/// <param name="Name">Display name of the security.</param>
/// <param name="MarketValue">Current market value of the position.</param>
/// <param name="Percentage">Share of the position relative to the total market value (0..1).</param>
/// <param name="UnrealizedGainLoss">Unrealized gain or loss of the position (market value minus invested capital).</param>
/// <returns>A top-position record.</returns>
public sealed record PortfolioTopPosition(Guid SecurityId, string Name, decimal MarketValue, decimal Percentage, decimal UnrealizedGainLoss);

/// <summary>
/// Bundles portfolio structure KPIs: market value, invested capital, unrealized gains/losses,
/// asset allocation, regional distribution, sector distribution and top-10 positions.
/// </summary>
/// <param name="TotalMarketValue">Total current market value across all positions.</param>
/// <param name="InvestedCapital">Total invested capital (FIFO cost basis of currently held shares) across all positions.</param>
/// <param name="UnrealizedGainLoss">Total unrealized gain/loss (TotalMarketValue - InvestedCapital).</param>
/// <param name="AssetAllocation">Market value distribution grouped by security category.</param>
/// <param name="RegionalDistribution">Market value distribution grouped by security region.</param>
/// <param name="SectorDistribution">Market value distribution grouped by security sector.</param>
/// <param name="TopPositions">Up to 10 largest positions ordered by market value descending.</param>
/// <returns>A portfolio structure record.</returns>
public sealed record PortfolioStructureDto(
    decimal TotalMarketValue,
    decimal InvestedCapital,
    decimal UnrealizedGainLoss,
    IReadOnlyList<PortfolioAllocationSlice> AssetAllocation,
    IReadOnlyList<PortfolioAllocationSlice> RegionalDistribution,
    IReadOnlyList<PortfolioAllocationSlice> SectorDistribution,
    IReadOnlyList<PortfolioTopPosition> TopPositions);

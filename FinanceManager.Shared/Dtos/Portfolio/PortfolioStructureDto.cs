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
/// A single position within the top-10-by-market-value list (or, when used for <see cref="PortfolioStructureDto.AllPositions"/>,
/// within the complete list of held positions).
/// </summary>
/// <param name="SecurityId">Identifier of the security.</param>
/// <param name="Name">Display name of the security.</param>
/// <param name="MarketValue">Current market value of the position.</param>
/// <param name="Percentage">Share of the position relative to the total market value (0..1).</param>
/// <param name="UnrealizedGainLoss">Unrealized gain or loss of the position (market value minus invested capital).</param>
/// <returns>A top-position record.</returns>
public sealed record PortfolioTopPosition(Guid SecurityId, string Name, decimal MarketValue, decimal Percentage, decimal UnrealizedGainLoss);

/// <summary>
/// A single remaining FIFO lot that contributed to a security's invested capital.
/// </summary>
/// <param name="PurchaseDate">Purchase date of the lot.</param>
/// <param name="Quantity">Number of shares still held from this lot.</param>
/// <param name="CostPerUnit">Cost per share for this lot (including linked fees).</param>
/// <param name="TotalCost">Total cost basis of this lot (Quantity * CostPerUnit).</param>
/// <returns>An invested-capital lot record.</returns>
public sealed record PortfolioInvestedCapitalLot(DateTime PurchaseDate, decimal Quantity, decimal CostPerUnit, decimal TotalCost);

/// <summary>
/// Invested capital of a single security together with the remaining FIFO lots (purchase postings)
/// that make up that amount.
/// </summary>
/// <param name="SecurityId">Identifier of the security.</param>
/// <param name="Name">Display name of the security.</param>
/// <param name="InvestedCapital">Invested capital of this security (sum of <paramref name="Lots"/>' total cost).</param>
/// <param name="Lots">Remaining FIFO lots contributing to the invested capital, ordered by purchase date descending.</param>
/// <returns>An invested-capital breakdown record for a single security.</returns>
public sealed record PortfolioInvestedCapitalPosition(Guid SecurityId, string Name, decimal InvestedCapital, IReadOnlyList<PortfolioInvestedCapitalLot> Lots);

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
/// <param name="AllPositions">All held positions ordered by market value descending, used for the total market value explanation.</param>
/// <param name="InvestedCapitalBreakdown">Per-security invested capital including the contributing FIFO lots, ordered by invested capital descending.</param>
/// <returns>A portfolio structure record.</returns>
public sealed record PortfolioStructureDto(
    decimal TotalMarketValue,
    decimal InvestedCapital,
    decimal UnrealizedGainLoss,
    IReadOnlyList<PortfolioAllocationSlice> AssetAllocation,
    IReadOnlyList<PortfolioAllocationSlice> RegionalDistribution,
    IReadOnlyList<PortfolioAllocationSlice> SectorDistribution,
    IReadOnlyList<PortfolioTopPosition> TopPositions,
    IReadOnlyList<PortfolioTopPosition> AllPositions,
    IReadOnlyList<PortfolioInvestedCapitalPosition> InvestedCapitalBreakdown);

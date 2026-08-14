using FinanceManager.Application.Portfolio;
using FinanceManager.Application.Securities.ReturnAnalysis;
using FinanceManager.Shared.Dtos.Portfolio;
using FinanceManager.Shared.Dtos.Securities;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Portfolio;

/// <summary>
/// Computes aggregated portfolio-level KPIs across all securities, postings and prices owned by a user.
/// Mirrors the single-security aggregation pattern of <c>ReturnAnalysisService</c>, but sums FIFO-based
/// position results across every security held by the user instead of a single one.
/// </summary>
public sealed class PortfolioAnalysisReportService : IPortfolioAnalysisReportService
{
    private readonly AppDbContext _db;
    private readonly IFifoCostBasisCalculator _fifo;
    private readonly IReturnCalculationService _calc;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortfolioAnalysisReportService"/> class.
    /// </summary>
    /// <param name="db">Application database context.</param>
    /// <param name="fifo">FIFO cost basis calculator, reused per-position from the single-security return analysis.</param>
    /// <param name="calc">Pure financial calculation service, reused for chain-linked time-weighted return.</param>
    public PortfolioAnalysisReportService(AppDbContext db, IFifoCostBasisCalculator fifo, IReturnCalculationService calc)
    {
        _db = db;
        _fifo = fifo;
        _calc = calc;
    }

    private const string UncategorizedLabel = "Ohne Kategorie";
    private const string UnknownRegionLabel = "Unbekannt";
    private const string UnknownSectorLabel = "Unbekannt";

    private sealed record PositionSnapshot(
        Guid SecurityId,
        string Name,
        Guid? CategoryId,
        string? CategoryName,
        string? Region,
        string? Sector,
        List<SecurityTransaction> Transactions,
        List<(DateTime Date, decimal Close)> Prices,
        FifoCostBasisResult Fifo,
        decimal CurrentPrice);

    /// <inheritdoc />
    public async Task<PortfolioAnalysisReportDto> GetPortfolioAnalysisReportAsync(Guid ownerUserId, CancellationToken ct)
    {
        var positions = await LoadPositionsAsync(ownerUserId, ct);
        var depotCashBalance = await LoadDepotCashBalanceAsync(ownerUserId, ct);

        var structure = BuildStructure(positions);
        var performance = BuildPerformance(positions);
        var cashflow = BuildCashflow(positions, depotCashBalance, structure.TotalMarketValue);
        var risk = new PortfolioRiskDto(null, null, null, null, null);

        var now = DateTime.UtcNow;
        var validUntil = EndOfMonthUtc(now);

        return new PortfolioAnalysisReportDto(structure, performance, cashflow, risk, now, validUntil);
    }

    /// <summary>
    /// Computes the UTC timestamp representing the end of the calendar month containing <paramref name="utcNow"/>.
    /// </summary>
    /// <param name="utcNow">Reference UTC timestamp.</param>
    /// <returns>UTC timestamp for 23:59:59.999 on the last day of the month.</returns>
    public static DateTime EndOfMonthUtc(DateTime utcNow)
    {
        var firstOfNextMonth = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        return firstOfNextMonth.AddTicks(-1);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Data loading
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<List<PositionSnapshot>> LoadPositionsAsync(Guid ownerUserId, CancellationToken ct)
    {
        var securities = await _db.Securities
            .AsNoTracking()
            .Where(s => s.OwnerUserId == ownerUserId)
            .Select(s => new { s.Id, s.Name, s.CategoryId, s.Region, s.Sector })
            .ToListAsync(ct);

        if (securities.Count == 0) { return []; }

        var categoryNames = await _db.SecurityCategories
            .AsNoTracking()
            .Where(c => c.OwnerUserId == ownerUserId)
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var securityIds = securities.Select(s => s.Id).ToList();

        var allTransactions = await _db.Postings
            .AsNoTracking()
            .Where(p => p.SecurityId != null && securityIds.Contains(p.SecurityId.Value) && p.SecuritySubType != null)
            .OrderBy(p => p.BookingDate)
            .ThenBy(p => p.Id)
            .Select(p => new { p.SecurityId, p.Id, p.BookingDate, Type = p.SecuritySubType!.Value, p.Amount, p.Quantity, p.GroupId })
            .ToListAsync(ct);

        var allPrices = await _db.SecurityPrices
            .AsNoTracking()
            .Where(p => securityIds.Contains(p.SecurityId))
            .OrderBy(p => p.Date)
            .Select(p => new { p.SecurityId, p.Date, p.Close })
            .ToListAsync(ct);

        var transactionsBySecurity = allTransactions
            .GroupBy(t => t.SecurityId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(t => new SecurityTransaction(t.Id, t.BookingDate, t.Type, t.Amount, t.Quantity, t.GroupId)).ToList());

        var pricesBySecurity = allPrices
            .GroupBy(p => p.SecurityId)
            .ToDictionary(g => g.Key, g => g.Select(p => (p.Date, p.Close)).ToList());

        var positions = new List<PositionSnapshot>(securities.Count);
        foreach (var s in securities)
        {
            var transactions = transactionsBySecurity.TryGetValue(s.Id, out var tx) ? tx : [];
            if (transactions.Count == 0) { continue; }

            var prices = pricesBySecurity.TryGetValue(s.Id, out var pr) ? pr : [];
            var fifo = _fifo.Calculate(transactions);
            var currentPrice = prices.Count > 0 ? prices[^1].Close : 0m;
            string? categoryName = s.CategoryId.HasValue && categoryNames.TryGetValue(s.CategoryId.Value, out var cn) ? cn : null;

            positions.Add(new PositionSnapshot(s.Id, s.Name, s.CategoryId, categoryName, s.Region, s.Sector, transactions, prices, fifo, currentPrice));
        }

        return positions;
    }

    private async Task<decimal> LoadDepotCashBalanceAsync(Guid ownerUserId, CancellationToken ct)
    {
        var securityIds = await _db.Securities
            .AsNoTracking()
            .Where(s => s.OwnerUserId == ownerUserId)
            .Select(s => s.Id)
            .ToListAsync(ct);

        if (securityIds.Count == 0)
        {
            return 0m;
        }

        var securityGroupIds = await _db.Postings
            .AsNoTracking()
            .Where(p => p.SecurityId != null
                        && securityIds.Contains(p.SecurityId.Value)
                        && p.SecuritySubType != null
                        && p.GroupId != Guid.Empty)
            .Select(p => p.GroupId)
            .Distinct()
            .ToListAsync(ct);

        if (securityGroupIds.Count == 0)
        {
            return 0m;
        }

        var accountIds = await _db.Postings
            .AsNoTracking()
            .Where(p => p.Kind == PostingKind.Bank
                        && p.AccountId != null
                        && securityGroupIds.Contains(p.GroupId))
            .Select(p => p.AccountId!.Value)
            .Distinct()
            .ToListAsync(ct);

        if (accountIds.Count == 0)
        {
            return 0m;
        }

        return await _db.Accounts
            .AsNoTracking()
            .Where(a => a.OwnerUserId == ownerUserId && accountIds.Contains(a.Id))
            .SumAsync(a => (decimal?)a.CurrentBalance, ct) ?? 0m;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Structure
    // ─────────────────────────────────────────────────────────────────────────

    private static PortfolioStructureDto BuildStructure(IReadOnlyList<PositionSnapshot> positions)
    {
        if (positions.Count == 0)
        {
            return new PortfolioStructureDto(0m, 0m, 0m, [], [], [], [], [], []);
        }

        decimal totalMarketValue = 0m;
        decimal totalInvestedCapital = 0m;

        var byCategory = new Dictionary<string, decimal>();
        var byRegion = new Dictionary<string, decimal>();
        var bySector = new Dictionary<string, decimal>();
        var allPositions = new List<PortfolioTopPosition>();
        var investedCapitalBreakdown = new List<PortfolioInvestedCapitalPosition>();

        foreach (var p in positions)
        {
            decimal marketValue = p.Fifo.TotalSharesHeld * p.CurrentPrice;
            decimal investedCapital = p.Fifo.TotalCostBasis - p.Fifo.StandaloneFeeTotal;

            totalMarketValue += marketValue;
            totalInvestedCapital += investedCapital;

            var categoryLabel = p.CategoryName ?? UncategorizedLabel;
            byCategory[categoryLabel] = byCategory.GetValueOrDefault(categoryLabel) + marketValue;

            var regionLabel = string.IsNullOrWhiteSpace(p.Region) ? UnknownRegionLabel : p.Region;
            byRegion[regionLabel] = byRegion.GetValueOrDefault(regionLabel) + marketValue;

            var sectorLabel = string.IsNullOrWhiteSpace(p.Sector) ? UnknownSectorLabel : p.Sector;
            bySector[sectorLabel] = bySector.GetValueOrDefault(sectorLabel) + marketValue;

            if (marketValue != 0m)
            {
                allPositions.Add(new PortfolioTopPosition(p.SecurityId, p.Name, marketValue, 0m, marketValue - investedCapital));
            }

            var breakdownEntry = BuildInvestedCapitalBreakdown(p.SecurityId, p.Name, p.Fifo, investedCapital);
            if (breakdownEntry is not null)
            {
                investedCapitalBreakdown.Add(breakdownEntry);
            }
        }

        var allPositionsSorted = allPositions
            .OrderByDescending(t => t.MarketValue)
            .Select(t => t with { Percentage = totalMarketValue != 0m ? t.MarketValue / totalMarketValue : 0m })
            .ToList();

        var top10 = allPositionsSorted.Take(10).ToList();

        investedCapitalBreakdown = investedCapitalBreakdown
            .OrderByDescending(i => i.InvestedCapital)
            .ToList();

        return new PortfolioStructureDto(
            totalMarketValue,
            totalInvestedCapital,
            totalMarketValue - totalInvestedCapital,
            ToSlices(byCategory, totalMarketValue),
            ToSlices(byRegion, totalMarketValue),
            ToSlices(bySector, totalMarketValue),
            top10,
            allPositionsSorted,
            investedCapitalBreakdown);
    }

    private static IReadOnlyList<PortfolioAllocationSlice> ToSlices(Dictionary<string, decimal> values, decimal total)
    {
        return values
            .Select(kv => new PortfolioAllocationSlice(kv.Key, kv.Value, total != 0m ? kv.Value / total : 0m))
            .OrderByDescending(s => s.Value)
            .ToList();
    }

    private static PortfolioInvestedCapitalPosition? BuildInvestedCapitalBreakdown(
        Guid securityId, string name, FifoCostBasisResult fifo, decimal investedCapital)
    {
        if (investedCapital == 0m && fifo.RemainingLots.Count == 0)
        {
            return null;
        }

        var lots = fifo.RemainingLots
            .Select(l => new PortfolioInvestedCapitalLot(l.PurchaseDate, l.Quantity, l.CostPerUnit, l.Quantity * l.CostPerUnit))
            .OrderByDescending(l => l.PurchaseDate)
            .ToList();

        return new PortfolioInvestedCapitalPosition(securityId, name, investedCapital, lots);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Performance
    // ─────────────────────────────────────────────────────────────────────────

    private PortfolioPerformanceDto BuildPerformance(IReadOnlyList<PositionSnapshot> positions)
    {
        if (positions.Count == 0)
        {
            return new PortfolioPerformanceDto(null, null, [], []);
        }

        var firstDate = positions.SelectMany(p => p.Transactions).Min(t => t.Date).Date;
        var today = DateTime.UtcNow.Date;
        int currentYear = today.Year;

        decimal sharesHeldToday = positions.Sum(p => SecurityValuationHelper.SharesHeldOnDate(p.Transactions, today));
        int lastYear = sharesHeldToday > 0m
            ? currentYear
            : positions.SelectMany(p => p.Transactions).Max(t => t.Date).Year;

        var annualReturns = new List<PortfolioAnnualReturnPoint>();
        var monthlyReturns = new List<PortfolioMonthlyReturnPoint>();
        var twrPeriods = new List<TwrPeriodInput>();
        decimal? ytdReturn = null;

        for (int year = firstDate.Year; year <= lastYear; year++)
        {
            bool isYtd = year == currentYear && sharesHeldToday > 0m;
            var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var yearEnd = isYtd ? today : new DateTime(year, 12, 31, 0, 0, 0, DateTimeKind.Utc);

            var (startValue, endValue, cashflow, periodReturn) = ComputePeriodMetrics(positions, yearStart, yearEnd);

            decimal yearReturn = periodReturn ?? 0m;
            annualReturns.Add(new PortfolioAnnualReturnPoint(year, yearReturn, isYtd));
            twrPeriods.Add(new TwrPeriodInput(yearStart, yearEnd, startValue, endValue, cashflow));
            if (isYtd) { ytdReturn = yearReturn; }

            int lastMonth = isYtd ? today.Month : 12;
            for (int month = 1; month <= lastMonth; month++)
            {
                var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
                var monthEnd = isYtd && month == today.Month
                    ? today
                    : new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc);

                var (_, _, _, monthReturn) = ComputePeriodMetrics(positions, monthStart, monthEnd);

                monthlyReturns.Add(new PortfolioMonthlyReturnPoint(year, month, monthReturn));
            }
        }

        decimal? twr = _calc.CalculateTwr(twrPeriods);

        return new PortfolioPerformanceDto(twr, ytdReturn, annualReturns, monthlyReturns);
    }

    /// <summary>
    /// Sums the market value of all positions on the given date using shares held and the latest known price at that date.
    /// Reuses <see cref="SecurityValuationHelper"/> so the underlying per-security calculation stays identical to
    /// the single-security return analysis.
    /// </summary>
    /// <param name="positions">All positions to sum.</param>
    /// <param name="date">Reference date (inclusive).</param>
    /// <returns>Total portfolio market value on the given date.</returns>
    private static decimal GetPortfolioValueOnDate(IReadOnlyList<PositionSnapshot> positions, DateTime date)
    {
        decimal total = 0m;
        foreach (var p in positions)
        {
            decimal shares = SecurityValuationHelper.SharesHeldOnDate(p.Transactions, date);
            if (shares == 0m) { continue; }
            total += shares * SecurityValuationHelper.LatestPriceOnOrBefore(p.Prices, date);
        }
        return total;
    }

    /// <summary>
    /// Sums external cashflows (Buy/Fee as inflow, Sell as outflow) across all positions within the given period (inclusive).
    /// Sign convention matches <see cref="IReturnCalculationService.CalculateTwr"/>: positive = capital committed to the portfolio.
    /// </summary>
    /// <param name="positions">All positions to sum.</param>
    /// <param name="start">Start of the period (inclusive).</param>
    /// <param name="end">End of the period (inclusive).</param>
    /// <returns>Net external cashflow across all positions within the period.</returns>
    private static decimal ExternalCashflowInPeriod(IReadOnlyList<PositionSnapshot> positions, DateTime start, DateTime end)
    {
        decimal cf = 0m;
        foreach (var p in positions)
        {
            foreach (var t in p.Transactions)
            {
                if (t.Date.Date < start.Date || t.Date.Date > end.Date) { continue; }
                if (t.Type == SecurityPostingSubType.Buy || t.Type == SecurityPostingSubType.Fee) { cf += Math.Abs(t.Amount); }
                else if (t.Type == SecurityPostingSubType.Sell) { cf -= t.Amount; }
            }
        }
        return cf;
    }

    /// <summary>
    /// Modified Dietz single-period return: (EndValue - StartValue - Cashflow) / (StartValue + 0.5 * Cashflow).
    /// Returns <c>null</c> when the denominator is zero (no capital deployed in the period).
    /// </summary>
    /// <param name="startValue">Portfolio value at the start of the period.</param>
    /// <param name="endValue">Portfolio value at the end of the period.</param>
    /// <param name="cashflow">Net external cashflow during the period (mid-period weighted).</param>
    /// <returns>Period return as a fraction, or <c>null</c> when the denominator is zero.</returns>
    private static decimal? ComputePeriodReturn(decimal startValue, decimal endValue, decimal cashflow)
    {
        decimal denominator = startValue + 0.5m * cashflow;
        if (denominator == 0m) { return null; }
        return (endValue - startValue - cashflow) / denominator;
    }

    // Computes the start value, end value, net external cashflow and Modified Dietz return for a single period.
    // Shared by the annual and monthly loops in BuildPerformance so both use identical start-/end-value,
    // cashflow and return computation logic. Returns (start value, end value, net cashflow, period return);
    // period return is null when undefined.
    private static (decimal, decimal, decimal, decimal?) ComputePeriodMetrics(
        IReadOnlyList<PositionSnapshot> positions, DateTime periodStart, DateTime periodEnd)
    {
        decimal startValue = GetPortfolioValueOnDate(positions, periodStart.AddDays(-1));
        decimal endValue = GetPortfolioValueOnDate(positions, periodEnd);
        decimal cashflow = ExternalCashflowInPeriod(positions, periodStart, periodEnd);
        decimal? periodReturn = ComputePeriodReturn(startValue, endValue, cashflow);
        return (startValue, endValue, cashflow, periodReturn);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cashflow
    // ─────────────────────────────────────────────────────────────────────────

    private PortfolioCashflowDto BuildCashflow(
        IReadOnlyList<PositionSnapshot> positions,
        decimal depotCashBalance,
        decimal totalMarketValue)
    {
        if (positions.Count == 0)
        {
            return new PortfolioCashflowDto(0m, 0m, 0m, 0m);
        }

        int currentYear = DateTime.UtcNow.Year;
        var yearStart = new DateTime(currentYear, 1, 1);

        decimal netDeposits = 0m;
        decimal dividends = 0m;
        decimal realizedGains = 0m;

        foreach (var p in positions)
        {
            var currentYearTx = p.Transactions.Where(t => t.Date.Year == currentYear).ToList();

            netDeposits += currentYearTx.Where(t => t.Type == SecurityPostingSubType.Buy).Sum(t => Math.Abs(t.Amount));
            netDeposits -= currentYearTx.Where(t => t.Type == SecurityPostingSubType.Sell).Sum(t => t.Amount);

            dividends += currentYearTx.Where(t => t.Type == SecurityPostingSubType.Dividend).Sum(t => t.Amount);

            // Realized gains this year = delta between FIFO realized gains up to now and FIFO realized gains
            // computed on transactions strictly before the current year (isolates sells within the year).
            var beforeYear = p.Transactions.Where(t => t.Date < yearStart).ToList();
            decimal realizedBeforeYear = beforeYear.Count > 0 ? _fifo.Calculate(beforeYear).RealizedGains : 0m;
            realizedGains += p.Fifo.RealizedGains - realizedBeforeYear;
        }

        return new PortfolioCashflowDto(netDeposits, dividends, realizedGains, CalculateLiquidityRatio(depotCashBalance, totalMarketValue));
    }

    private static decimal CalculateLiquidityRatio(decimal depotCashBalance, decimal totalMarketValue)
    {
        if (totalMarketValue <= 0m)
        {
            return 0m;
        }

        var denominator = totalMarketValue + depotCashBalance;
        return denominator > 0m ? depotCashBalance / denominator : 0m;
    }
}

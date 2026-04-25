using FinanceManager.Application.Securities.ReturnAnalysis;
using FinanceManager.Domain.Postings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Securities.ReturnAnalysis;

/// <summary>
/// Orchestrates return analysis for a single security. Implements <see cref="IReturnAnalysisService"/>.
/// All methods are user-scoped and cache results for <see cref="CacheTtl"/> (1 hour) by default.
/// Database ownership is enforced by joining postings and price data through the Security entity (Blocker S-1).
/// </summary>
public sealed class ReturnAnalysisService : IReturnAnalysisService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly AppDbContext _db;
    private readonly IReturnCalculationService _calc;
    private readonly IFifoCostBasisCalculator _fifo;
    private readonly IReturnAnalysisCache _cache;
    private readonly ILogger<ReturnAnalysisService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ReturnAnalysisService"/>.
    /// </summary>
    /// <param name="db">Application database context.</param>
    /// <param name="calc">Pure financial calculations service.</param>
    /// <param name="fifo">FIFO cost basis calculator.</param>
    /// <param name="cache">Return analysis cache abstraction.</param>
    /// <param name="logger">Logger instance.</param>
    public ReturnAnalysisService(
        AppDbContext db,
        IReturnCalculationService calc,
        IFifoCostBasisCalculator fifo,
        IReturnAnalysisCache cache,
        ILogger<ReturnAnalysisService> logger)
    {
        _db = db;
        _calc = calc;
        _fifo = fifo;
        _cache = cache;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IReturnAnalysisService
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<ReturnSummaryDto?> GetReturnSummaryAsync(Guid securityId, Guid ownerUserId, CancellationToken ct)
    {
        var key = ReturnAnalysisCacheKeys.Summary(securityId, ownerUserId);
        return _cache.GetOrCreateAsync(key, () => ComputeReturnSummaryAsync(securityId, ownerUserId, ct), CacheTtl);
    }

    /// <inheritdoc/>
    public Task<SparklineDataDto?> GetSparklineDataAsync(Guid securityId, Guid ownerUserId, CancellationToken ct)
    {
        var key = ReturnAnalysisCacheKeys.Sparkline(securityId, ownerUserId);
        return _cache.GetOrCreateAsync(key, () => ComputeSparklineDataAsync(securityId, ownerUserId, ct), CacheTtl);
    }

    /// <inheritdoc/>
    public Task<DetailedReturnMetricsDto?> GetDetailedMetricsAsync(Guid securityId, Guid ownerUserId, CancellationToken ct)
    {
        var key = ReturnAnalysisCacheKeys.Metrics(securityId, ownerUserId);
        return _cache.GetOrCreateAsync(key, () => ComputeDetailedMetricsAsync(securityId, ownerUserId, ct), CacheTtl);
    }

    /// <inheritdoc/>
    public Task<PeriodicReturnsDto?> GetPeriodicReturnsAsync(Guid securityId, Guid ownerUserId, CancellationToken ct)
    {
        var key = ReturnAnalysisCacheKeys.Periodic(securityId, ownerUserId);
        return _cache.GetOrCreateAsync(key, () => ComputePeriodicReturnsAsync(securityId, ownerUserId, ct), CacheTtl);
    }

    /// <inheritdoc/>
    public Task<CashflowTimelineDto?> GetCashflowTimelineAsync(Guid securityId, Guid ownerUserId, CancellationToken ct)
    {
        var key = ReturnAnalysisCacheKeys.Cashflow(securityId, ownerUserId);
        return _cache.GetOrCreateAsync(key, () => ComputeCashflowTimelineAsync(securityId, ownerUserId, ct), CacheTtl);
    }

    /// <inheritdoc/>
    public Task<PerformanceChartDataDto?> GetPerformanceChartDataAsync(Guid securityId, Guid ownerUserId, ChartTimeRange timeRange, CancellationToken ct)
    {
        var key = ReturnAnalysisCacheKeys.Chart(securityId, ownerUserId, timeRange.ToString());
        return _cache.GetOrCreateAsync(key, () => ComputePerformanceChartDataAsync(securityId, ownerUserId, timeRange, ct), CacheTtl);
    }

    /// <inheritdoc/>
    public Task<BenchmarkComparisonDto?> GetBenchmarkComparisonAsync(Guid securityId, Guid ownerUserId, CancellationToken ct)
    {
        var key = ReturnAnalysisCacheKeys.Benchmark(securityId, ownerUserId);
        return _cache.GetOrCreateAsync(key, () => ComputeBenchmarkComparisonAsync(securityId, ownerUserId, ct), CacheTtl);
    }

    /// <inheritdoc/>
    public async Task<ReturnAnalysisSettingsDto?> GetUserSettingsAsync(Guid ownerUserId, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == ownerUserId, ct);

        if (user == null) return null;

        string? benchmarkName = null;
        if (user.BenchmarkSecurityId.HasValue)
        {
            benchmarkName = await _db.Securities
                .AsNoTracking()
                .Where(s => s.Id == user.BenchmarkSecurityId.Value && s.OwnerUserId == ownerUserId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(ct);
        }

        return new ReturnAnalysisSettingsDto(
            user.BenchmarkSecurityId,
            benchmarkName,
            user.ShowSharpeRatio,
            user.RiskFreeRate);
    }

    /// <inheritdoc/>
    public async Task UpdateUserSettingsAsync(Guid ownerUserId, Guid? benchmarkSecurityId, bool showSharpeRatio, decimal riskFreeRate, CancellationToken ct)
    {
        // S-3: verify benchmark security ownership
        if (benchmarkSecurityId.HasValue)
        {
            bool benchmarkOwned = await _db.Securities
                .AnyAsync(s => s.Id == benchmarkSecurityId.Value && s.OwnerUserId == ownerUserId, ct);

            if (!benchmarkOwned)
            {
                _logger.LogWarning(
                    "UpdateUserSettingsAsync: Benchmark security {BenchmarkId} not found or not owned by user {UserId}.",
                    benchmarkSecurityId.Value, ownerUserId);
                throw new ArgumentException("Benchmark security not found or not owned by this user.", nameof(benchmarkSecurityId));
            }
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == ownerUserId, ct);
        if (user == null)
        {
            _logger.LogWarning("UpdateUserSettingsAsync: User {UserId} not found.", ownerUserId);
            return;
        }

        user.SetReturnAnalysisSettings(benchmarkSecurityId, showSharpeRatio, riskFreeRate);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public Task InvalidateCacheAsync(Guid securityId, Guid ownerUserId)
    {
        var token = ReturnAnalysisCacheKeys.SecurityUserToken(securityId, ownerUserId);
        return _cache.InvalidateAsync(token);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private computation methods
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<ReturnSummaryDto?> ComputeReturnSummaryAsync(
        Guid securityId, Guid ownerUserId, CancellationToken ct)
    {
        // Verify security exists and is owned by user
        var security = await _db.Securities
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == securityId && s.OwnerUserId == ownerUserId, ct);

        if (security == null) return null;

        var transactions = await LoadPostingsAsync(securityId, ownerUserId, ct);
        if (transactions.Count == 0)
        {
            _logger.LogDebug("ComputeReturnSummaryAsync: No transactions for security {SecurityId}.", securityId);
            return null;
        }

        var fifoResult = _fifo.Calculate(transactions);

        // Latest price
        var latestPrice = await _db.SecurityPrices
            .AsNoTracking()
            .Where(sp => sp.SecurityId == securityId)
            .OrderByDescending(sp => sp.Date)
            .Select(sp => new { sp.Date, sp.Close })
            .FirstOrDefaultAsync(ct);

        decimal currentPricePerShare = latestPrice?.Close ?? 0m;
        decimal sharesHeld = fifoResult.TotalSharesHeld;
        decimal currentMarketValue = sharesHeld * currentPricePerShare;
        decimal investedCapital = fifoResult.TotalCostBasis;

        // Net dividends = sum of Dividend postings - sum of Tax postings (taxes reduce dividend income)
        decimal grossDividends = transactions
            .Where(t => t.Type == SecurityPostingSubType.Dividend)
            .Sum(t => t.Amount);
        decimal taxes = transactions
            .Where(t => t.Type == SecurityPostingSubType.Tax)
            .Sum(t => Math.Abs(t.Amount));
        decimal netDividends = grossDividends - taxes;

        decimal? totalReturnPercent = _calc.CalculateTotalReturn(investedCapital, currentMarketValue, netDividends);
        decimal totalReturnAbsolute = currentMarketValue + netDividends - investedCapital;

        // CAGR: from first buy date to today
        var firstBuy = transactions
            .Where(t => t.Type == SecurityPostingSubType.Buy)
            .OrderBy(t => t.Date)
            .FirstOrDefault();

        decimal? cagr = null;
        if (firstBuy != null && investedCapital > 0m)
        {
            double years = (DateTime.Today - firstBuy.Date.Date).TotalDays / 365.25;
            if (years >= 1.0)
            {
                cagr = _calc.CalculateCagr(investedCapital, currentMarketValue + netDividends, years);
            }
        }

        // IRR cashflows: buys are negative outflows, sells + dividends - taxes are positive inflows
        // Current market value is the terminal inflow
        var irrCashflows = BuildIrrCashflows(transactions, currentMarketValue, DateTime.Today);
        decimal? irr = _calc.CalculateIrr(irrCashflows);

        decimal costBasisPerShare = sharesHeld > 0m
            ? investedCapital / sharesHeld
            : 0m;

        bool hasMissingPrices = latestPrice == null
            || (fifoResult.HasOversellWarning);

        string? missingPricesHint = latestPrice == null
            ? "Kein aktueller Kurs verfügbar."
            : fifoResult.HasOversellWarning
                ? fifoResult.OversellWarningMessage
                : null;

        return new ReturnSummaryDto(
            InvestedCapital: investedCapital,
            CurrentMarketValue: currentMarketValue,
            TotalReturnAbsolute: totalReturnAbsolute,
            TotalReturnPercent: totalReturnPercent ?? 0m,
            Cagr: cagr,
            Irr: irr,
            CostBasisPerShare: costBasisPerShare,
            CurrentPricePerShare: currentPricePerShare,
            NetDividends: netDividends,
            CurrencyCode: security.CurrencyCode,
            HasMissingPrices: hasMissingPrices,
            MissingPricesHint: missingPricesHint);
    }

    private async Task<SparklineDataDto?> ComputeSparklineDataAsync(
        Guid securityId, Guid ownerUserId, CancellationToken ct)
    {
        var transactions = await LoadPostingsAsync(securityId, ownerUserId, ct);
        if (transactions.Count == 0) return null;

        var firstDate = transactions.Min(t => t.Date).Date;
        var prices = await LoadPriceHistoryAsync(securityId, ownerUserId, firstDate, DateTime.Today, ct);

        if (prices.Count < 30) return null;

        var filledPrices = ForwardFill(prices, firstDate, DateTime.Today);

        var points = BuildSparklinePoints(transactions, filledPrices);
        return new SparklineDataDto(points);
    }

    private async Task<DetailedReturnMetricsDto?> ComputeDetailedMetricsAsync(
        Guid securityId, Guid ownerUserId, CancellationToken ct)
    {
        var security = await _db.Securities
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == securityId && s.OwnerUserId == ownerUserId, ct);

        if (security == null) return null;

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == ownerUserId, ct);

        if (user == null) return null;

        var transactions = await LoadPostingsAsync(securityId, ownerUserId, ct);
        if (transactions.Count == 0) return null;

        var fifoResult = _fifo.Calculate(transactions);

        var firstDate = transactions.Min(t => t.Date).Date;
        var prices = await LoadPriceHistoryAsync(securityId, ownerUserId, firstDate, DateTime.Today, ct);
        var filledPrices = ForwardFill(prices, firstDate, DateTime.Today);

        var latestPrice = filledPrices.Count > 0 ? filledPrices[^1].Close : 0m;
        decimal sharesHeld = fifoResult.TotalSharesHeld;
        decimal currentMarketValue = sharesHeld * latestPrice;
        decimal investedCapital = fifoResult.TotalCostBasis;

        decimal grossDividends = transactions
            .Where(t => t.Type == SecurityPostingSubType.Dividend)
            .Sum(t => t.Amount);
        decimal totalTaxes = transactions
            .Where(t => t.Type == SecurityPostingSubType.Tax)
            .Sum(t => Math.Abs(t.Amount));
        decimal totalFees = transactions
            .Where(t => t.Type == SecurityPostingSubType.Fee)
            .Sum(t => Math.Abs(t.Amount));
        decimal netDividends = grossDividends - totalTaxes;

        decimal grossReturn = currentMarketValue + grossDividends - investedCapital;
        decimal netReturn = currentMarketValue + netDividends - investedCapital;
        decimal taxRate = _calc.CalculateTaxRate(totalTaxes, grossReturn) ?? 0m;

        // TWR
        var twrPeriods = BuildTwrPeriods(transactions, filledPrices);
        decimal? twr = _calc.CalculateTwr(twrPeriods);

        // Volatility & MaxDrawdown from portfolio values
        var portfolioValues = BuildPortfolioValueSeries(transactions, filledPrices);
        decimal? volatility = portfolioValues.Count >= 2
            ? _calc.CalculateVolatility(portfolioValues)
            : null;
        decimal? maxDrawdown = portfolioValues.Count >= 2
            ? _calc.CalculateMaxDrawdown(portfolioValues)
            : null;

        // Sharpe Ratio (opt-in)
        decimal? sharpeRatio = null;
        if (user.ShowSharpeRatio && twr.HasValue && volatility.HasValue && volatility.Value > 0m)
        {
            sharpeRatio = _calc.CalculateSharpeRatio(twr.Value, user.RiskFreeRate, volatility.Value);
        }

        decimal unrealizedGains = currentMarketValue - investedCapital;

        // IRR
        var irrCashflows = BuildIrrCashflows(transactions, currentMarketValue, DateTime.Today);
        decimal? irr = _calc.CalculateIrr(irrCashflows);

        // Dividend yield current year
        int currentYear = DateTime.Today.Year;
        decimal currentYearDividends = transactions
            .Where(t => t.Type == SecurityPostingSubType.Dividend && t.Date.Year == currentYear)
            .Sum(t => t.Amount);
        decimal dividendYieldCurrentYear = _calc.CalculateDividendYield(currentYearDividends, investedCapital) ?? 0m;

        return new DetailedReturnMetricsDto(
            GrossReturn: grossReturn,
            NetReturn: netReturn,
            TotalTaxes: totalTaxes,
            TotalFees: totalFees,
            TaxRate: taxRate,
            Twr: twr,
            Volatility: volatility,
            MaxDrawdown: maxDrawdown,
            SharpeRatio: sharpeRatio,
            RealizedGains: fifoResult.RealizedGains,
            UnrealizedGains: unrealizedGains,
            Irr: irr,
            DividendYieldCurrentYear: dividendYieldCurrentYear);
    }

    private async Task<PeriodicReturnsDto?> ComputePeriodicReturnsAsync(
        Guid securityId, Guid ownerUserId, CancellationToken ct)
    {
        bool securityExists = await _db.Securities
            .AsNoTracking()
            .AnyAsync(s => s.Id == securityId && s.OwnerUserId == ownerUserId, ct);

        if (!securityExists) return null;

        var transactions = await LoadPostingsAsync(securityId, ownerUserId, ct);
        if (transactions.Count == 0) return null;

        var firstDate = transactions.Min(t => t.Date).Date;
        var prices = await LoadPriceHistoryAsync(securityId, ownerUserId, firstDate, DateTime.Today, ct);
        var filledPrices = ForwardFill(prices, firstDate, DateTime.Today);

        int currentYear = DateTime.Today.Year;
        int firstYear = firstDate.Year;

        var annualReturns = new List<AnnualReturnPoint>();
        var monthlyReturns = new List<MonthlyReturnPoint>();

        for (int year = firstYear; year <= currentYear; year++)
        {
            bool isYtd = year == currentYear;

            // Annual return: compare start-of-year portfolio value to end-of-year
            var yearStartDate = new DateTime(year, 1, 1);
            var yearEndDate = isYtd ? DateTime.Today : new DateTime(year, 12, 31);

            decimal startValue = GetPortfolioValueOnDate(transactions, filledPrices, yearStartDate);
            decimal endValue = GetPortfolioValueOnDate(transactions, filledPrices, yearEndDate);

            decimal annualReturn = startValue > 0m
                ? (endValue - startValue) / startValue
                : 0m;

            annualReturns.Add(new AnnualReturnPoint(year, annualReturn * 100m, isYtd));

            // Monthly returns
            int lastMonth = isYtd ? DateTime.Today.Month : 12;
            for (int month = 1; month <= lastMonth; month++)
            {
                var monthStart = new DateTime(year, month, 1);
                var monthEnd = isYtd && month == DateTime.Today.Month
                    ? DateTime.Today
                    : new DateTime(year, month, DateTime.DaysInMonth(year, month));

                decimal mStartValue = GetPortfolioValueOnDate(transactions, filledPrices, monthStart);
                decimal mEndValue = GetPortfolioValueOnDate(transactions, filledPrices, monthEnd);

                decimal? monthReturn = mStartValue > 0m
                    ? (mEndValue - mStartValue) / mStartValue * 100m
                    : null;

                monthlyReturns.Add(new MonthlyReturnPoint(year, month, monthReturn));
            }
        }

        // Annual dividends
        var annualDividends = BuildAnnualDividends(transactions, firstYear, currentYear);

        return new PeriodicReturnsDto(
            AnnualReturns: annualReturns.AsReadOnly(),
            MonthlyReturns: monthlyReturns.AsReadOnly(),
            AnnualDividends: annualDividends);
    }

    private async Task<CashflowTimelineDto?> ComputeCashflowTimelineAsync(
        Guid securityId, Guid ownerUserId, CancellationToken ct)
    {
        bool securityExists = await _db.Securities
            .AsNoTracking()
            .AnyAsync(s => s.Id == securityId && s.OwnerUserId == ownerUserId, ct);

        if (!securityExists) return null;

        var transactions = await LoadPostingsAsync(securityId, ownerUserId, ct);
        if (transactions.Count == 0)
            return new CashflowTimelineDto(
                Array.Empty<CashflowEntry>(),
                Array.Empty<AnnualCashflowSummary>());

        var entries = transactions.Select(t => new CashflowEntry(
            Date: t.Date,
            Type: t.Type.ToString(),
            Amount: t.Amount,
            Description: null,
            PostingId: t.Id)).ToList();

        // Annual summaries
        var annualGroups = transactions
            .GroupBy(t => t.Date.Year)
            .OrderBy(g => g.Key);

        var annualSummaries = annualGroups.Select(g =>
        {
            decimal totalBuys = g.Where(t => t.Type == SecurityPostingSubType.Buy).Sum(t => t.Amount);
            decimal totalSells = g.Where(t => t.Type == SecurityPostingSubType.Sell).Sum(t => t.Amount);
            decimal totalDividends = g.Where(t => t.Type == SecurityPostingSubType.Dividend).Sum(t => t.Amount);
            decimal totalTaxes = g.Where(t => t.Type == SecurityPostingSubType.Tax).Sum(t => t.Amount);
            decimal totalFees = g.Where(t => t.Type == SecurityPostingSubType.Fee).Sum(t => t.Amount);

            return new AnnualCashflowSummary(g.Key, totalBuys, totalSells, totalDividends, totalTaxes, totalFees);
        }).ToList();

        return new CashflowTimelineDto(entries.AsReadOnly(), annualSummaries.AsReadOnly());
    }

    private async Task<PerformanceChartDataDto?> ComputePerformanceChartDataAsync(
        Guid securityId, Guid ownerUserId, ChartTimeRange timeRange, CancellationToken ct)
    {
        bool securityExists = await _db.Securities
            .AsNoTracking()
            .AnyAsync(s => s.Id == securityId && s.OwnerUserId == ownerUserId, ct);

        if (!securityExists) return null;

        var transactions = await LoadPostingsAsync(securityId, ownerUserId, ct);
        if (transactions.Count == 0) return null;

        var fromDate = GetFromDateForTimeRange(timeRange);
        var prices = await LoadPriceHistoryAsync(securityId, ownerUserId, fromDate, DateTime.Today, ct);
        var filledPrices = ForwardFill(prices, fromDate, DateTime.Today);

        if (filledPrices.Count == 0) return null;

        var portfolioValues = new List<ChartPoint>(filledPrices.Count);
        var investedCapitalValues = new List<ChartPoint>(filledPrices.Count);

        // Build running invested capital per day
        var allTransactions = await LoadPostingsAsync(securityId, ownerUserId, ct);
        foreach (var (date, close) in filledPrices)
        {
            decimal sharesHeld = ComputeSharesHeldOnDate(allTransactions, date);
            decimal marketValue = sharesHeld * close;
            decimal investedCapital = ComputeInvestedCapitalOnDate(allTransactions, date);

            portfolioValues.Add(new ChartPoint(date, marketValue));
            investedCapitalValues.Add(new ChartPoint(date, investedCapital));
        }

        return new PerformanceChartDataDto(
            TimeRange: timeRange,
            PortfolioValues: portfolioValues.AsReadOnly(),
            InvestedCapitalValues: investedCapitalValues.AsReadOnly());
    }

    private async Task<BenchmarkComparisonDto?> ComputeBenchmarkComparisonAsync(
        Guid securityId, Guid ownerUserId, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == ownerUserId, ct);

        if (user?.BenchmarkSecurityId == null) return null;

        var benchmarkId = user.BenchmarkSecurityId.Value;

        // S-3: Verify benchmark security is owned by the same user
        var benchmarkSecurity = await _db.Securities
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == benchmarkId && s.OwnerUserId == ownerUserId, ct);

        if (benchmarkSecurity == null)
        {
            _logger.LogWarning(
                "ComputeBenchmarkComparisonAsync: Benchmark security {BenchmarkId} not found or not owned by user {UserId}.",
                benchmarkId, ownerUserId);
            return null;
        }

        // Load price history for the target security (from first transaction)
        var transactions = await LoadPostingsAsync(securityId, ownerUserId, ct);
        if (transactions.Count == 0) return null;

        var firstDate = transactions.Min(t => t.Date).Date;

        var securityPrices = await LoadPriceHistoryAsync(securityId, ownerUserId, firstDate, DateTime.Today, ct);
        if (securityPrices.Count < 2) return null;

        // Load benchmark price history for the same period
        var benchmarkPrices = await LoadPriceHistoryAsync(benchmarkId, ownerUserId, firstDate, DateTime.Today, ct);
        if (benchmarkPrices.Count < 2) return null;

        // Normalize both to base 100 at the earliest common date
        var securityFilled = ForwardFill(securityPrices, firstDate, DateTime.Today);
        var benchmarkFilled = ForwardFill(benchmarkPrices, firstDate, DateTime.Today);

        if (securityFilled.Count == 0 || benchmarkFilled.Count == 0) return null;

        decimal securityBase = securityFilled[0].Close;
        decimal benchmarkBase = benchmarkFilled[0].Close;

        if (securityBase == 0m || benchmarkBase == 0m) return null;

        var securityNormalized = securityFilled
            .Select(p => new ChartPoint(p.Date, p.Close / securityBase * 100m))
            .ToList();

        var benchmarkNormalized = benchmarkFilled
            .Select(p => new ChartPoint(p.Date, p.Close / benchmarkBase * 100m))
            .ToList();

        return new BenchmarkComparisonDto(
            BenchmarkSecurityId: benchmarkId,
            BenchmarkName: benchmarkSecurity.Name,
            SecurityNormalizedValues: securityNormalized.AsReadOnly(),
            BenchmarkNormalizedValues: benchmarkNormalized.AsReadOnly());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Data loading helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads all security transactions for the given security, enforcing ownership via JOIN on Security (Blocker S-1).
    /// </summary>
    private async Task<List<SecurityTransaction>> LoadPostingsAsync(
        Guid securityId, Guid ownerUserId, CancellationToken ct)
    {
        // S-1: Posting has no OwnerUserId → ownership enforced by JOIN through Security.OwnerUserId
        return await _db.Postings
            .AsNoTracking()
            .Join(
                _db.Securities,
                p => p.SecurityId,
                s => (Guid?)s.Id,
                (p, s) => new { p, s })
            .Where(x =>
                x.p.SecurityId == securityId
                && x.s.OwnerUserId == ownerUserId
                && x.p.SecuritySubType != null)
            .OrderBy(x => x.p.BookingDate)
            .ThenBy(x => x.p.Id)
            .Select(x => new SecurityTransaction(
                x.p.Id,
                x.p.BookingDate,
                x.p.SecuritySubType!.Value,
                x.p.Amount,
                x.p.Quantity,
                x.p.GroupId))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Loads the price history for a security in the given date range, enforcing ownership via JOIN.
    /// </summary>
    private async Task<List<(DateTime Date, decimal Close)>> LoadPriceHistoryAsync(
        Guid securityId, Guid ownerUserId, DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await _db.SecurityPrices
            .AsNoTracking()
            .Join(
                _db.Securities,
                sp => sp.SecurityId,
                s => s.Id,
                (sp, s) => new { sp, s })
            .Where(x =>
                x.sp.SecurityId == securityId
                && x.s.OwnerUserId == ownerUserId
                && x.sp.Date >= from.Date
                && x.sp.Date <= to.Date)
            .OrderBy(x => x.sp.Date)
            .Select(x => new { x.sp.Date, x.sp.Close })
            .ToListAsync(ct);

        return rows.Select(r => (r.Date, r.Close)).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Computation helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Forward-fills price data to fill gaps (weekends, holidays). Returns only dates with prices.
    /// </summary>
    private static List<(DateTime Date, decimal Close)> ForwardFill(
        List<(DateTime Date, decimal Close)> prices, DateTime from, DateTime to)
    {
        if (prices.Count == 0) return prices;

        var result = new List<(DateTime Date, decimal Close)>();
        var priceMap = prices.ToDictionary(p => p.Date.Date, p => p.Close);
        decimal? lastPrice = null;

        for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
        {
            if (priceMap.TryGetValue(date, out var price))
            {
                lastPrice = price;
            }
            if (lastPrice.HasValue)
            {
                result.Add((date, lastPrice.Value));
            }
        }

        return result;
    }

    /// <summary>
    /// Builds IRR cashflows from transactions. Buys are negative, sells and dividends (net of taxes) are positive.
    /// The current market value is added as the terminal inflow on <paramref name="terminalDate"/>.
    /// </summary>
    private static IReadOnlyList<CashflowPoint> BuildIrrCashflows(
        IReadOnlyList<SecurityTransaction> transactions,
        decimal currentMarketValue,
        DateTime terminalDate)
    {
        var cashflows = new List<CashflowPoint>();

        foreach (var tx in transactions)
        {
            // Buys: amount is negative (outflow) by domain convention
            // Sells, Dividends: amount is positive (inflow)
            // Taxes, Fees: negative (outflow)
            cashflows.Add(new CashflowPoint(tx.Date, tx.Amount));
        }

        // Terminal inflow = current market value of remaining shares
        if (currentMarketValue > 0m)
        {
            cashflows.Add(new CashflowPoint(terminalDate, currentMarketValue));
        }

        return cashflows.AsReadOnly();
    }

    /// <summary>
    /// Builds TWR periods from transactions and forward-filled prices.
    /// Each period spans between consecutive cashflow events.
    /// </summary>
    private static IReadOnlyList<TwrPeriodInput> BuildTwrPeriods(
        IReadOnlyList<SecurityTransaction> transactions,
        IReadOnlyList<(DateTime Date, decimal Close)> filledPrices)
    {
        if (filledPrices.Count == 0 || transactions.Count == 0)
            return Array.Empty<TwrPeriodInput>();

        var priceMap = filledPrices.ToDictionary(p => p.Date.Date, p => p.Close);

        // Cashflow event dates (Buy, Sell only – dividends are not portfolio cashflows for TWR)
        var cashflowDates = transactions
            .Where(t => t.Type == SecurityPostingSubType.Buy || t.Type == SecurityPostingSubType.Sell)
            .Select(t => t.Date.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (cashflowDates.Count == 0) return Array.Empty<TwrPeriodInput>();

        var periods = new List<TwrPeriodInput>();
        var allDates = filledPrices.Select(p => p.Date.Date).ToList();

        // Build a running snapshot of shares held at any date
        DateTime? lastEventDate = null;

        foreach (var eventDate in cashflowDates)
        {
            if (lastEventDate == null)
            {
                lastEventDate = eventDate;
                continue;
            }

            var start = lastEventDate.Value;
            var end = eventDate;

            if (!priceMap.TryGetValue(start, out var startPrice) ||
                !priceMap.TryGetValue(end, out var endPrice))
            {
                lastEventDate = eventDate;
                continue;
            }

            decimal sharesAtStart = ComputeSharesHeldOnDate(transactions, start);
            decimal sharesAtEnd = ComputeSharesHeldOnDate(transactions, start); // before end-of-day cashflow

            decimal startValue = sharesAtStart * startPrice;
            decimal endValue = sharesAtEnd * endPrice;

            // External cashflow on end date: net buy/sell amounts
            decimal externalCashflow = transactions
                .Where(t => t.Date.Date == end
                         && (t.Type == SecurityPostingSubType.Buy || t.Type == SecurityPostingSubType.Sell))
                .Sum(t => t.Amount);

            periods.Add(new TwrPeriodInput(start, end, startValue, endValue, externalCashflow));
            lastEventDate = eventDate;
        }

        // Final period: from last event to today
        if (lastEventDate.HasValue && allDates.Count > 0)
        {
            var finalStart = lastEventDate.Value;
            var finalEnd = allDates[^1];

            if (priceMap.TryGetValue(finalStart, out var fStartPrice) &&
                priceMap.TryGetValue(finalEnd, out var fEndPrice) &&
                finalStart < finalEnd)
            {
                decimal shares = ComputeSharesHeldOnDate(transactions, finalEnd);
                decimal startValue = shares * fStartPrice;
                decimal endValue = shares * fEndPrice;

                periods.Add(new TwrPeriodInput(finalStart, finalEnd, startValue, endValue, 0m));
            }
        }

        return periods.AsReadOnly();
    }

    /// <summary>
    /// Builds sparkline data points from transactions and forward-filled prices.
    /// </summary>
    private static IReadOnlyList<SparklinePoint> BuildSparklinePoints(
        IReadOnlyList<SecurityTransaction> transactions,
        IReadOnlyList<(DateTime Date, decimal Close)> filledPrices)
    {
        var points = new List<SparklinePoint>(filledPrices.Count);

        foreach (var (date, close) in filledPrices)
        {
            decimal shares = ComputeSharesHeldOnDate(transactions, date);
            decimal marketValue = shares * close;
            decimal investedCapital = ComputeInvestedCapitalOnDate(transactions, date);
            points.Add(new SparklinePoint(date, marketValue, investedCapital));
        }

        return points.AsReadOnly();
    }

    /// <summary>
    /// Builds a list of portfolio market values (for volatility / max-drawdown calculation).
    /// </summary>
    private static List<decimal> BuildPortfolioValueSeries(
        IReadOnlyList<SecurityTransaction> transactions,
        IReadOnlyList<(DateTime Date, decimal Close)> filledPrices)
    {
        var result = new List<decimal>(filledPrices.Count);
        foreach (var (date, close) in filledPrices)
        {
            decimal shares = ComputeSharesHeldOnDate(transactions, date);
            result.Add(shares * close);
        }
        return result;
    }

    /// <summary>
    /// Computes shares held on or before a given date from transaction history.
    /// </summary>
    private static decimal ComputeSharesHeldOnDate(
        IReadOnlyList<SecurityTransaction> transactions, DateTime date)
    {
        decimal shares = 0m;
        foreach (var tx in transactions)
        {
            if (tx.Date.Date > date.Date) break;
            if (tx.Type == SecurityPostingSubType.Buy)
                shares += tx.Quantity ?? 0m;
            else if (tx.Type == SecurityPostingSubType.Sell)
                shares -= tx.Quantity ?? 0m;
        }
        return Math.Max(0m, shares);
    }

    /// <summary>
    /// Computes invested capital (cost basis of remaining shares) on a given date using a simple running sum.
    /// For a precise FIFO basis, use FifoCostBasisCalculator on filtered transactions.
    /// </summary>
    private static decimal ComputeInvestedCapitalOnDate(
        IReadOnlyList<SecurityTransaction> transactions, DateTime date)
    {
        decimal capital = 0m;
        foreach (var tx in transactions)
        {
            if (tx.Date.Date > date.Date) break;
            if (tx.Type == SecurityPostingSubType.Buy)
                capital += Math.Abs(tx.Amount);
            else if (tx.Type == SecurityPostingSubType.Sell)
            {
                // Reduce proportionally – approximation (full FIFO would need lot tracking per day)
                capital -= tx.Amount; // sell amount is positive, so subtract
            }
        }
        return Math.Max(0m, capital);
    }

    /// <summary>
    /// Returns the portfolio market value on a given date by interpolating from the filled price series.
    /// </summary>
    private static decimal GetPortfolioValueOnDate(
        IReadOnlyList<SecurityTransaction> transactions,
        IReadOnlyList<(DateTime Date, decimal Close)> filledPrices,
        DateTime date)
    {
        // Find the closest price on or before the requested date
        decimal? close = null;
        foreach (var (d, c) in filledPrices)
        {
            if (d.Date <= date.Date) close = c;
            else break;
        }

        if (close == null) return 0m;
        decimal shares = ComputeSharesHeldOnDate(transactions, date);
        return shares * close.Value;
    }

    /// <summary>
    /// Builds annual dividend summary list.
    /// </summary>
    private static IReadOnlyList<AnnualDividendPoint> BuildAnnualDividends(
        IReadOnlyList<SecurityTransaction> transactions, int firstYear, int lastYear)
    {
        decimal cumulativeNet = 0m;
        var result = new List<AnnualDividendPoint>();

        for (int year = firstYear; year <= lastYear; year++)
        {
            decimal gross = transactions
                .Where(t => t.Type == SecurityPostingSubType.Dividend && t.Date.Year == year)
                .Sum(t => t.Amount);

            decimal taxes = transactions
                .Where(t => t.Type == SecurityPostingSubType.Tax && t.Date.Year == year)
                .Sum(t => Math.Abs(t.Amount));

            decimal net = gross - taxes;
            cumulativeNet += net;

            result.Add(new AnnualDividendPoint(year, gross, net, cumulativeNet));
        }

        return result.AsReadOnly();
    }

    /// <summary>
    /// Converts a <see cref="ChartTimeRange"/> to an absolute from-date.
    /// </summary>
    private static DateTime GetFromDateForTimeRange(ChartTimeRange timeRange)
    {
        var today = DateTime.Today;
        return timeRange switch
        {
            ChartTimeRange.OneMonth => today.AddMonths(-1),
            ChartTimeRange.ThreeMonths => today.AddMonths(-3),
            ChartTimeRange.SixMonths => today.AddMonths(-6),
            ChartTimeRange.OneYear => today.AddYears(-1),
            ChartTimeRange.ThreeYears => today.AddYears(-3),
            ChartTimeRange.All => DateTime.MinValue,
            _ => today.AddYears(-1)
        };
    }
}

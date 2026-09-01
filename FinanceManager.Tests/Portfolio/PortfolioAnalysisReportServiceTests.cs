using FinanceManager.Application.Securities.ReturnAnalysis;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Contacts;
using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Securities;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Portfolio;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Portfolio;

/// <summary>
/// Integration tests for <see cref="PortfolioAnalysisReportService"/> covering portfolio structure
/// aggregation, grouping and user-scoping. Uses EF Core InMemory database with a fresh instance per test
/// and the real <see cref="FifoCostBasisCalculator"/> / <see cref="ReturnCalculationService"/> implementations.
/// </summary>
public sealed class PortfolioAnalysisReportServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PortfolioAnalysisReportService _sut;

    /// <summary>
    /// Sets up a fresh in-memory <see cref="AppDbContext"/> and wires the service under test with
    /// real <see cref="FifoCostBasisCalculator"/> and <see cref="ReturnCalculationService"/>
    /// implementations, so tests exercise actual cost-basis and return math rather than mocked-out
    /// approximations.
    /// </summary>
    public PortfolioAnalysisReportServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var fifo = new FifoCostBasisCalculator(NullLogger<FifoCostBasisCalculator>.Instance);
        var calc = new ReturnCalculationService(NullLogger<ReturnCalculationService>.Instance);
        _sut = new PortfolioAnalysisReportService(_db, fifo, calc);
    }

    /// <summary>Releases the in-memory <see cref="AppDbContext"/> used by each test.</summary>
    public void Dispose() => _db.Dispose();

    private User CreateUser()
    {
        var user = new User($"user-{Guid.NewGuid():N}", "hash");
        _db.Users.Add(user);
        return user;
    }

    private Security CreateSecurity(Guid ownerUserId, string name, Guid? categoryId = null, string? region = null, string? sector = null)
    {
        var security = new Security(ownerUserId, name, name.ToUpperInvariant(), null, null, "EUR", categoryId, region, sector);
        _db.Securities.Add(security);
        return security;
    }

    private async Task<Account> CreateAccountAsync(Guid ownerUserId, decimal currentBalance, string name = "Depot Cash")
    {
        var bank = new Contact(ownerUserId, $"Bank-{Guid.NewGuid():N}", ContactType.Bank, null, null);
        _db.Contacts.Add(bank);
        await _db.SaveChangesAsync();

        var account = new Account(ownerUserId, AccountType.Giro, name, null, bank.Id);
        if (currentBalance != 0m)
        {
            account.AdjustBalance(currentBalance);
        }

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();
        return account;
    }

    private void AddBuyPosting(Guid securityId, DateTime date, decimal amount, decimal quantity, Guid? groupId = null)
    {
        var posting = new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, securityId, date, -Math.Abs(amount), null, null, null, SecurityPostingSubType.Buy, quantity);
        if (groupId.HasValue)
        {
            posting.SetGroup(groupId.Value);
        }

        _db.Postings.Add(posting);
    }

    private void AddSellPosting(Guid securityId, DateTime date, decimal amount, decimal quantity)
        => _db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, securityId, date, Math.Abs(amount), null, null, null, SecurityPostingSubType.Sell, quantity));

    private void AddDividendPosting(Guid securityId, DateTime date, decimal amount)
        => _db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, securityId, date, Math.Abs(amount), null, null, null, SecurityPostingSubType.Dividend, null));

    private void AddBankPosting(Guid accountId, Guid groupId, DateTime date, decimal amount = 0m)
        => _db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Bank, accountId, null, null, null, date, amount).SetGroup(groupId));

    private void AddPrice(Guid securityId, DateTime date, decimal close)
        => _db.SecurityPrices.Add(new SecurityPrice(securityId, date, close));

    /// <summary>
    /// Verifies the baseline structure calculation for a single held security: market value,
    /// invested capital, and unrealized gain/loss are computed correctly from one buy and a current
    /// price, and the position shows up in the top-positions list.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_SingleSecurity_ReturnsCorrectStructure()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        AddBuyPosting(security.Id, DateTime.Today.AddYears(-1), 1000m, 10m);
        AddPrice(security.Id, DateTime.Today, 120m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Structure.TotalMarketValue.Should().Be(1200m);
        report.Structure.InvestedCapital.Should().Be(1000m);
        report.Structure.UnrealizedGainLoss.Should().Be(200m);
        report.Structure.TopPositions.Should().ContainSingle(p => p.SecurityId == security.Id && p.MarketValue == 1200m);
    }

    /// <summary>
    /// Verifies that positions are aggregated correctly across category, region, and sector
    /// dimensions - including that a security without an assigned category is grouped under the
    /// "Ohne Kategorie" (uncategorized) bucket rather than being dropped or misgrouped.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_MultipleCategoriesRegionsSectors_GroupsCorrectly()
    {
        var user = CreateUser();
        var category = new SecurityCategory(user.Id, "Aktien");
        _db.SecurityCategories.Add(category);

        var s1 = CreateSecurity(user.Id, "Apple", category.Id, "Nordamerika", "Technologie");
        var s2 = CreateSecurity(user.Id, "SAP", null, "Europa", "Technologie");

        AddBuyPosting(s1.Id, DateTime.Today.AddYears(-1), 1000m, 10m);
        AddPrice(s1.Id, DateTime.Today, 100m);
        AddBuyPosting(s2.Id, DateTime.Today.AddYears(-1), 500m, 5m);
        AddPrice(s2.Id, DateTime.Today, 100m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Structure.AssetAllocation.Should().Contain(a => a.Label == "Aktien" && a.Value == 1000m);
        report.Structure.AssetAllocation.Should().Contain(a => a.Label == "Ohne Kategorie" && a.Value == 500m);
        report.Structure.RegionalDistribution.Should().Contain(r => r.Label == "Nordamerika" && r.Value == 1000m);
        report.Structure.RegionalDistribution.Should().Contain(r => r.Label == "Europa" && r.Value == 500m);
        report.Structure.SectorDistribution.Should().ContainSingle(sc => sc.Label == "Technologie" && sc.Value == 1500m);
    }

    /// <summary>
    /// Verifies that a dividend posting in the current year is reflected in
    /// <c>Cashflow.DividendsCurrentYear</c>, confirming dividend income is picked up separately
    /// from the buy/sell postings that drive market value.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_WithDividends_CashflowCalculatedCorrectly()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        AddBuyPosting(security.Id, DateTime.Today.AddYears(-1), 1000m, 10m);
        AddDividendPosting(security.Id, DateTime.Today, 42m);
        AddPrice(security.Id, DateTime.Today, 100m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Cashflow.DividendsCurrentYear.Should().Be(42m);
    }

    /// <summary>
    /// Verifies that a buy posting linked (via posting group) to a bank cash withdrawal is
    /// correctly attributed to the associated depot's cash account, so the liquidity ratio
    /// (cash balance versus total market value) reflects the actual depot cash rather than
    /// treating the withdrawal as unrelated to the security purchase.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_WithDepotCashPostingGroup_CalculatesLiquidityRatio()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        var account = await CreateAccountAsync(user.Id, 500m);
        var groupId = Guid.NewGuid();

        AddBuyPosting(security.Id, DateTime.Today.AddYears(-1), 1000m, 10m, groupId);
        AddBankPosting(account.Id, groupId, DateTime.Today.AddYears(-1), -1000m);
        AddPrice(security.Id, DateTime.Today, 100m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Structure.TotalMarketValue.Should().Be(1000m);
        report.Cashflow.LiquidityCashBalance.Should().Be(500m);
        report.Cashflow.LiquidityTotalMarketValue.Should().Be(1000m);
        report.Cashflow.LiquidityRatio.Should().Be(500m / 1500m);
    }

    /// <summary>
    /// Verifies that when the same cash account is linked to two different security purchase
    /// groups, its balance is counted once in the liquidity calculation rather than being summed
    /// once per linked group - a regression guard against double-counting shared account cash.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_MultipleSecurityGroupsForSameAccount_DeduplicatesCashBalance()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        var account = await CreateAccountAsync(user.Id, 250m);
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();

        AddBuyPosting(security.Id, DateTime.Today.AddYears(-2), 500m, 5m, groupA);
        AddBuyPosting(security.Id, DateTime.Today.AddYears(-1), 500m, 5m, groupB);
        AddBankPosting(account.Id, groupA, DateTime.Today.AddYears(-2), -500m);
        AddBankPosting(account.Id, groupB, DateTime.Today.AddYears(-1), -500m);
        AddPrice(security.Id, DateTime.Today, 100m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Structure.TotalMarketValue.Should().Be(1000m);
        report.Cashflow.LiquidityCashBalance.Should().Be(250m);
        report.Cashflow.LiquidityTotalMarketValue.Should().Be(1000m);
        report.Cashflow.LiquidityRatio.Should().Be(250m / 1250m);
    }

    /// <summary>
    /// Verifies strict user-scoping of the liquidity calculation: another user's accounts, groups,
    /// and postings - even when they share a group id with the target user's own postings - must
    /// not contribute to the target user's cash balance or market value.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_ForeignAccountsAndGroups_DoNotAffectLiquidityRatio()
    {
        var user = CreateUser();
        var otherUser = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        var otherSecurity = CreateSecurity(otherUser.Id, "SAP");
        var ownAccount = await CreateAccountAsync(user.Id, 200m, "Own Cash");
        var foreignAccount = await CreateAccountAsync(otherUser.Id, 900m, "Foreign Cash");
        var ownGroup = Guid.NewGuid();
        var foreignGroup = Guid.NewGuid();

        AddBuyPosting(security.Id, DateTime.Today.AddYears(-1), 800m, 8m, ownGroup);
        AddBankPosting(ownAccount.Id, ownGroup, DateTime.Today.AddYears(-1), -800m);
        AddBankPosting(foreignAccount.Id, ownGroup, DateTime.Today.AddYears(-1), -800m);
        AddBuyPosting(otherSecurity.Id, DateTime.Today.AddYears(-1), 500m, 5m, foreignGroup);
        AddBankPosting(foreignAccount.Id, foreignGroup, DateTime.Today.AddYears(-1), -500m);
        AddPrice(security.Id, DateTime.Today, 100m);
        AddPrice(otherSecurity.Id, DateTime.Today, 100m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Structure.TotalMarketValue.Should().Be(800m);
        report.Cashflow.LiquidityCashBalance.Should().Be(200m);
        report.Cashflow.LiquidityTotalMarketValue.Should().Be(800m);
        report.Cashflow.LiquidityRatio.Should().Be(200m / 1000m);
    }

    /// <summary>
    /// Verifies that when a user has no linked cash account at all, the liquidity ratio is reported
    /// as zero rather than null or an error - distinguishing "no cash available" from the
    /// "ratio cannot be computed" case covered by the negative/closed-position tests below.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_NoCashAccount_ReturnsZeroLiquidityRatio()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        AddBuyPosting(security.Id, DateTime.Today.AddYears(-1), 1000m, 10m, Guid.NewGuid());
        AddPrice(security.Id, DateTime.Today, 100m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Cashflow.LiquidityCashBalance.Should().Be(0m);
        report.Cashflow.LiquidityTotalMarketValue.Should().Be(1000m);
        report.Cashflow.LiquidityRatio.Should().Be(0m);
    }

    /// <summary>
    /// Verifies that a negative depot cash balance (the account is overdrawn) makes the liquidity
    /// ratio unavailable (<see langword="null"/>) rather than returning a negative or misleading
    /// ratio value that would not make sense to a user reading the report.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_NegativeDepotCash_ReturnsUnavailableLiquidityRatio()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        var account = await CreateAccountAsync(user.Id, -1000m);
        var groupId = Guid.NewGuid();

        AddBuyPosting(security.Id, DateTime.Today.AddYears(-1), 1000m, 10m, groupId);
        AddBankPosting(account.Id, groupId, DateTime.Today.AddYears(-1), -1000m);
        AddPrice(security.Id, DateTime.Today, 100m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Structure.TotalMarketValue.Should().Be(1000m);
        report.Cashflow.LiquidityCashBalance.Should().Be(-1000m);
        report.Cashflow.LiquidityTotalMarketValue.Should().Be(1000m);
        report.Cashflow.LiquidityRatio.Should().BeNull();
    }

    /// <summary>
    /// Verifies that once a position has been fully sold (zero market value) despite positive cash
    /// in the linked depot, the liquidity ratio is unavailable rather than reporting a nonsensical
    /// zero-over-cash figure - a ratio only makes sense while the position is still open.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_ClosedPositionWithPositiveDepotCash_ReturnsUnavailableLiquidityRatio()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        var account = await CreateAccountAsync(user.Id, 500m);
        var groupId = Guid.NewGuid();

        AddBuyPosting(security.Id, DateTime.Today.AddYears(-1), 1000m, 10m, groupId);
        AddBankPosting(account.Id, groupId, DateTime.Today.AddYears(-1), -1000m);
        AddSellPosting(security.Id, DateTime.Today, 1000m, 10m);
        AddPrice(security.Id, DateTime.Today, 100m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Structure.TotalMarketValue.Should().Be(0m);
        report.Cashflow.LiquidityCashBalance.Should().Be(500m);
        report.Cashflow.LiquidityTotalMarketValue.Should().Be(0m);
        report.Cashflow.LiquidityRatio.Should().BeNull();
    }

    /// <summary>
    /// Verifies that a user with a security defined but no postings at all gets a well-formed,
    /// entirely empty report (zero totals, empty collections) instead of a null-reference failure
    /// or throwing exception.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_NoPostings_ReturnsEmptyStructure()
    {
        var user = CreateUser();
        CreateSecurity(user.Id, "Apple");
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Structure.TotalMarketValue.Should().Be(0m);
        report.Structure.InvestedCapital.Should().Be(0m);
        report.Structure.TopPositions.Should().BeEmpty();
        report.Structure.AssetAllocation.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies core user-scoping: when two users each hold different securities, requesting one
    /// user's report returns only that user's position and never leaks the other user's holdings
    /// into the top-positions list.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_MultipleUsers_OnlyReturnsOwnData()
    {
        var userA = CreateUser();
        var userB = CreateUser();

        var securityA = CreateSecurity(userA.Id, "Apple");
        AddBuyPosting(securityA.Id, DateTime.Today.AddYears(-1), 1000m, 10m);
        AddPrice(securityA.Id, DateTime.Today, 100m);

        var securityB = CreateSecurity(userB.Id, "SAP");
        AddBuyPosting(securityB.Id, DateTime.Today.AddYears(-1), 5000m, 50m);
        AddPrice(securityB.Id, DateTime.Today, 200m);

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var reportA = await _sut.GetPortfolioAnalysisReportAsync(userA.Id, CancellationToken.None);

        reportA.Structure.TotalMarketValue.Should().Be(1000m);
        reportA.Structure.TopPositions.Should().ContainSingle(p => p.SecurityId == securityA.Id);
        reportA.Structure.TopPositions.Should().NotContain(p => p.SecurityId == securityB.Id);
    }

    /// <summary>
    /// Verifies that the full <c>AllPositions</c> list (as opposed to the capped top-positions list)
    /// contains every non-zero position sorted strictly by market value descending, so consumers of
    /// the full list can rely on ordering without re-sorting.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_AllPositions_ContainsAllNonZeroPositionsSortedByMarketValueDescending()
    {
        var user = CreateUser();

        var s1 = CreateSecurity(user.Id, "Apple");
        AddBuyPosting(s1.Id, DateTime.Today.AddYears(-1), 1000m, 10m);
        AddPrice(s1.Id, DateTime.Today, 100m); // market value 1000

        var s2 = CreateSecurity(user.Id, "SAP");
        AddBuyPosting(s2.Id, DateTime.Today.AddYears(-1), 500m, 5m);
        AddPrice(s2.Id, DateTime.Today, 300m); // market value 1500

        var s3 = CreateSecurity(user.Id, "Siemens");
        AddBuyPosting(s3.Id, DateTime.Today.AddYears(-1), 200m, 2m);
        AddPrice(s3.Id, DateTime.Today, 100m); // market value 200

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Structure.AllPositions.Should().HaveCount(3);
        report.Structure.AllPositions.Select(p => p.SecurityId).Should().ContainInOrder(s2.Id, s1.Id, s3.Id);
        report.Structure.AllPositions.Select(p => p.MarketValue).Should().BeInDescendingOrder();
        report.Structure.AllPositions.Should().OnlyContain(p => p.MarketValue != 0m);
    }

    /// <summary>
    /// Verifies that with more than ten positions held, <c>TopPositions</c> is exactly the first ten
    /// entries of <c>AllPositions</c> in the same order - confirming the top-10 cap is a simple
    /// truncation of the already-sorted full list rather than a separately computed ranking.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_TopPositions_EqualsAllPositionsTakeTen()
    {
        var user = CreateUser();

        var securities = new List<Security>();
        for (int i = 0; i < 12; i++)
        {
            var security = CreateSecurity(user.Id, $"Security{i:D2}");
            securities.Add(security);
            decimal quantity = 1m;
            decimal price = 100m + i; // distinct market values, no ties
            AddBuyPosting(security.Id, DateTime.Today.AddYears(-1), quantity * price, quantity);
            AddPrice(security.Id, DateTime.Today, price);
        }

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Structure.AllPositions.Should().HaveCount(12);
        report.Structure.TopPositions.Should().HaveCount(10);
        report.Structure.TopPositions.Should().BeEquivalentTo(
            report.Structure.AllPositions.Take(10),
            options => options.WithStrictOrdering());
    }

    /// <summary>
    /// Verifies FIFO lot tracking for the invested-capital breakdown: a fee posting linked to a buy
    /// via the same posting group is folded into that lot's total cost, the sum of all lots equals
    /// the security's total invested capital, and lots are ordered by purchase date descending -
    /// so the newest tax lot is shown first.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_InvestedCapitalBreakdown_LotsSumMatchesInvestedCapitalAndSortedByPurchaseDateDescending()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");

        var olderBuy = new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, DateTime.Today.AddYears(-2), -1000m, null, null, null, SecurityPostingSubType.Buy, 10m);
        var groupId = Guid.NewGuid();
        olderBuy.SetGroup(groupId);
        _db.Postings.Add(olderBuy);

        var linkedFee = new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, security.Id, DateTime.Today.AddYears(-2), -10m, null, null, null, SecurityPostingSubType.Fee, null);
        linkedFee.SetGroup(groupId);
        _db.Postings.Add(linkedFee);

        AddBuyPosting(security.Id, DateTime.Today.AddYears(-1), 500m, 5m); // newer lot, no fee
        AddPrice(security.Id, DateTime.Today, 100m);

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        var breakdown = report.Structure.InvestedCapitalBreakdown.Should().ContainSingle(b => b.SecurityId == security.Id).Subject;

        breakdown.Lots.Sum(l => l.TotalCost).Should().Be(breakdown.InvestedCapital);
        breakdown.Lots.Select(l => l.PurchaseDate).Should().BeInDescendingOrder();
    }

    /// <summary>
    /// Verifies that a security whose entire position has been sold off no longer appears in the
    /// invested-capital breakdown, since there is no remaining lot to attribute invested capital to
    /// once the FIFO quantity has been fully depleted.
    /// </summary>
    [Fact]
    public async Task GetPortfolioReport_InvestedCapitalBreakdown_FullySoldSecurity_IsExcluded()
    {
        var user = CreateUser();
        var security = CreateSecurity(user.Id, "Apple");
        AddBuyPosting(security.Id, DateTime.Today.AddYears(-2), 1000m, 10m);
        AddSellPosting(security.Id, DateTime.Today.AddYears(-1), 1200m, 10m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = await _sut.GetPortfolioAnalysisReportAsync(user.Id, CancellationToken.None);

        report.Structure.InvestedCapitalBreakdown.Should().NotContain(b => b.SecurityId == security.Id);
    }
}

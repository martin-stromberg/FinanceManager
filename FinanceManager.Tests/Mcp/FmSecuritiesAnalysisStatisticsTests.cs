using FinanceManager.Application.Securities;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.Mcp;
using FinanceManager.Web.Mcp.Tools;
using Moq;
using System.Text.Json;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Handler tests of the <c>fm_securities_analysis</c> opt-in <c>includeStatistics</c>
/// aggregate: currency/category market values, position counts, positions without a
/// usable price, currency purity of totals and filter semantics (the statistics set
/// equals the record-scope/filtered set and is not affected by <c>skip</c>/<c>take</c>).
/// </summary>
public sealed class FmSecuritiesAnalysisStatisticsTests : McpToolTestBase
{
    private static SecurityDto NewSecurityDto(
        string currency = "EUR", decimal? openQuantity = null,
        Guid? categoryId = null, string? categoryName = null, bool isActive = true)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Identifier = "ISIN",
            CurrencyCode = currency,
            CategoryId = categoryId,
            CategoryName = categoryName,
            OpenQuantity = openQuantity,
            IsActive = isActive,
            CreatedUtc = DateTime.UtcNow
        };

    private static FmSecuritiesAnalysisHandler NewHandler(
        ISecurityService securities, ISecurityPriceService prices, McpRecordScopeFilter filter,
        IFxRateService? fxRates = null)
        => new(securities, prices, Mock.Of<ISecurityCategoryService>(), filter, fxRates ?? Mock.Of<IFxRateService>());

    private static Mock<ISecurityService> SecuritiesReturning(Guid owner, params SecurityDto[] items)
    {
        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.ListAsync(owner, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items.Where(i => i.IsActive).ToList());
        // Real service semantics: onlyActive=false is the unfiltered "all" call.
        securities.Setup(s => s.ListAsync(owner, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items.ToList());
        return securities;
    }

    private static Mock<ISecurityPriceService> PricesReturning(Guid owner, params (Guid Id, decimal Close, DateTime Date)[] rows)
    {
        var prices = new Mock<ISecurityPriceService>();
        prices.Setup(p => p.GetLatestPricesAsync(owner, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                rows.Where(r => ids.Contains(r.Id)).ToDictionary(r => r.Id, r => new SecurityPriceDto(r.Date, r.Close)));
        return prices;
    }

    /// <summary>
    /// Without <c>includeStatistics</c> the response carries <c>statistics: null</c>
    /// (existing contract unchanged).
    /// </summary>
    [Fact]
    public async Task Invoke_WithoutIncludeStatistics_EmitsNullStatistics()
    {
        var owner = Guid.NewGuid();
        var handler = NewHandler(
            SecuritiesReturning(owner, NewSecurityDto(openQuantity: 5m)).Object,
            PricesReturning(owner).Object, NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("statistics").ValueKind);
    }

    /// <summary>
    /// The aggregate covers the filtered set: securities with zero quantity count into
    /// <c>securityCount</c> but not <c>positionCount</c>; market values group per currency
    /// and a mixed-currency portfolio emits no misleading cross-currency total.
    /// </summary>
    [Fact]
    public async Task Invoke_IncludeStatistics_GroupsByCurrency_AndKeepsTotalCurrencyPure()
    {
        var owner = Guid.NewGuid();
        var eur = NewSecurityDto(currency: "EUR", openQuantity: 10m);
        var usd = NewSecurityDto(currency: "USD", openQuantity: 4m);
        var empty = NewSecurityDto(currency: "EUR", openQuantity: 0m);
        var date = new DateTime(2026, 11, 20);

        var handler = NewHandler(
            SecuritiesReturning(owner, eur, usd, empty).Object,
            PricesReturning(owner, (eur.Id, 100m, date), (usd.Id, 50m, date)).Object,
            NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true,\"includeStatistics\":true}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var stats = doc.RootElement.GetProperty("statistics");
        Assert.Equal(3, stats.GetProperty("securityCount").GetInt32());
        Assert.Equal(2, stats.GetProperty("positionCount").GetInt32());
        Assert.Equal(0, stats.GetProperty("positionsWithoutPrice").GetProperty("count").GetInt32());

        var byCurrency = stats.GetProperty("byCurrency");
        Assert.Equal(2, byCurrency.GetArrayLength());
        var eurRow = byCurrency.EnumerateArray().Single(r => r.GetProperty("currencyCode").GetString() == "EUR");
        Assert.Equal(1000m, eurRow.GetProperty("marketValue").GetDecimal());
        var usdRow = byCurrency.EnumerateArray().Single(r => r.GetProperty("currencyCode").GetString() == "USD");
        Assert.Equal(200m, usdRow.GetProperty("marketValue").GetDecimal());

        Assert.Equal(JsonValueKind.Null, stats.GetProperty("totalMarketValue").ValueKind);
        Assert.Equal(JsonValueKind.Null, stats.GetProperty("totalCurrencyCode").ValueKind);
        Assert.Equal(date, stats.GetProperty("oldestPriceDate").GetDateTime());
        Assert.Equal(date, stats.GetProperty("newestPriceDate").GetDateTime());
    }

    /// <summary>
    /// Single-currency portfolios expose <c>totalMarketValue</c>/<c>totalCurrencyCode</c>;
    /// a position without a stored close is counted in <c>positionsWithoutPrice</c> and
    /// excluded from market values.
    /// </summary>
    [Fact]
    public async Task Invoke_IncludeStatistics_SingleCurrencyTotal_AndUnpricedPositions()
    {
        var owner = Guid.NewGuid();
        var priced = NewSecurityDto(currency: "EUR", openQuantity: 10m);
        var unpriced = NewSecurityDto(currency: "EUR", openQuantity: 3m);
        var date = new DateTime(2026, 11, 20);

        var handler = NewHandler(
            SecuritiesReturning(owner, priced, unpriced).Object,
            PricesReturning(owner, (priced.Id, 100m, date)).Object,
            NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true,\"includeStatistics\":true}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var stats = doc.RootElement.GetProperty("statistics");
        Assert.Equal(1000m, stats.GetProperty("totalMarketValue").GetDecimal());
        Assert.Equal("EUR", stats.GetProperty("totalCurrencyCode").GetString());

        var unpricedEntry = stats.GetProperty("positionsWithoutPrice");
        Assert.Equal(1, unpricedEntry.GetProperty("count").GetInt32());
        Assert.Equal(unpriced.Id, unpricedEntry.GetProperty("securityIds")[0].GetGuid());
        Assert.Equal(1, stats.GetProperty("byCurrency")[0].GetProperty("positionsWithoutPrice").GetInt32());
    }

    /// <summary>
    /// Category groups carry the name under <c>untrusted_data</c>; a category mixing
    /// valued currencies emits <c>marketValue</c>/<c>currencyCode</c> as <c>null</c>.
    /// </summary>
    [Fact]
    public async Task Invoke_IncludeStatistics_ByCategory_UntrustedName_AndMixedCurrencyNull()
    {
        var owner = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var eur = NewSecurityDto(currency: "EUR", openQuantity: 10m, categoryId: categoryId, categoryName: "ETFs");
        var usd = NewSecurityDto(currency: "USD", openQuantity: 4m, categoryId: categoryId, categoryName: "ETFs");
        var date = new DateTime(2026, 11, 20);

        var handler = NewHandler(
            SecuritiesReturning(owner, eur, usd).Object,
            PricesReturning(owner, (eur.Id, 100m, date), (usd.Id, 50m, date)).Object,
            NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true,\"includeStatistics\":true}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var byCategory = doc.RootElement.GetProperty("statistics").GetProperty("byCategory");
        Assert.Equal(1, byCategory.GetArrayLength());
        var row = byCategory[0];
        Assert.Equal(categoryId, row.GetProperty("categoryId").GetGuid());
        Assert.Equal(2, row.GetProperty("positionCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("marketValue").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("currencyCode").ValueKind);
        Assert.Equal("ETFs", row.GetProperty("untrusted_data").GetProperty("name").GetString());
        Assert.Throws<KeyNotFoundException>(() => row.GetProperty("name"));
    }

    /// <summary>
    /// <c>skip</c>/<c>take</c> narrow only the emitted list — the statistics still cover
    /// the full visible set, and the price batch runs over the full set's ids.
    /// </summary>
    [Fact]
    public async Task Invoke_IncludeStatistics_SkipTakeDoNotAffectStatistics()
    {
        var owner = Guid.NewGuid();
        var first = NewSecurityDto(currency: "EUR", openQuantity: 1m);
        var second = NewSecurityDto(currency: "EUR", openQuantity: 2m);
        var date = new DateTime(2026, 11, 20);

        var securities = SecuritiesReturning(owner, first, second);
        var prices = PricesReturning(owner, (first.Id, 10m, date), (second.Id, 20m, date));

        var handler = NewHandler(securities.Object, prices.Object, NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true,\"includeStatistics\":true,\"take\":1}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        Assert.Equal(1, doc.RootElement.GetProperty("securities").GetArrayLength());
        var stats = doc.RootElement.GetProperty("statistics");
        Assert.Equal(2, stats.GetProperty("securityCount").GetInt32());
        Assert.Equal(50m, stats.GetProperty("totalMarketValue").GetDecimal());

        prices.Verify(p => p.GetLatestPricesAsync(owner,
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The record-scope filter applies to the statistics: a category-filtered position
    /// is absent from counts and values.
    /// </summary>
    [Fact]
    public async Task Invoke_IncludeStatistics_RecordScopeFiltersAggregation()
    {
        var owner = Guid.NewGuid();
        var access = NewAccess(owner, McpAccessLevel.Individuell);
        var allowedCategory = Guid.NewGuid();
        var scope = new McpAccessScope(access.Id, owner, McpDataType.Securities, McpRightKind.Read,
            securityCategoryIdsCsv: allowedCategory.ToString());

        var visible = NewSecurityDto(currency: "EUR", openQuantity: 10m, categoryId: allowedCategory);
        var filteredOut = NewSecurityDto(currency: "EUR", openQuantity: 99m, categoryId: Guid.NewGuid());
        var date = new DateTime(2026, 11, 20);

        var handler = NewHandler(
            SecuritiesReturning(owner, visible, filteredOut).Object,
            PricesReturning(owner, (visible.Id, 100m, date), (filteredOut.Id, 1m, date)).Object,
            NewFilter(scope));
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true,\"includeStatistics\":true}"),
            NewCallContext(NewAccessContext(owner, access).Object), CancellationToken.None));

        var stats = doc.RootElement.GetProperty("statistics");
        Assert.Equal(1, stats.GetProperty("securityCount").GetInt32());
        Assert.Equal(1000m, stats.GetProperty("totalMarketValue").GetDecimal());
    }

    /// <summary>
    /// <c>onlyActive: false</c> lists only inactive securities (regression: the
    /// unfiltered service call must not return actives, and omitted
    /// <c>onlyActive</c> must not duplicate them).
    /// </summary>
    [Fact]
    public async Task Invoke_OnlyActiveFalse_ReturnsOnlyInactive()
    {
        var owner = Guid.NewGuid();
        var active = NewSecurityDto(isActive: true);
        var inactive = NewSecurityDto(isActive: false);

        var handler = NewHandler(
            SecuritiesReturning(owner, active, inactive).Object,
            PricesReturning(owner).Object, NewFilter());

        using var docFalse = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":false}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        var listFalse = docFalse.RootElement.GetProperty("securities");
        Assert.Equal(1, listFalse.GetArrayLength());
        Assert.Equal(inactive.Id, listFalse[0].GetProperty("id").GetGuid());

        using var docOmitted = Serialize(await handler.InvokeAsync(
            Args("{}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));
        var listAll = docOmitted.RootElement.GetProperty("securities");
        Assert.Equal(2, listAll.GetArrayLength());
        Assert.Equal(2, listAll.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).Distinct().Count());
    }

    /// <summary>
    /// The single-security path aggregates exactly that one security.
    /// </summary>
    [Fact]
    public async Task Invoke_IncludeStatistics_SecurityId_AggregatesSingleSecurity()
    {
        var owner = Guid.NewGuid();
        var security = NewSecurityDto(currency: "EUR", openQuantity: 2m);
        var date = new DateTime(2026, 11, 20);

        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.GetAsync(security.Id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(security);
        var prices = PricesReturning(owner, (security.Id, 150m, date));
        prices.Setup(p => p.ListAsync(owner, security.Id, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecurityPriceDto>());

        var handler = NewHandler(securities.Object, prices.Object, NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"securityId\":\"{security.Id}\",\"includeStatistics\":true}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var stats = doc.RootElement.GetProperty("statistics");
        Assert.Equal(1, stats.GetProperty("securityCount").GetInt32());
        Assert.Equal(300m, stats.GetProperty("totalMarketValue").GetDecimal());
    }
}

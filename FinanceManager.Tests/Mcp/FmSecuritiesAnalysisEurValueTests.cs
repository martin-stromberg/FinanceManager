using FinanceManager.Application.Securities;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.Mcp;
using FinanceManager.Web.Mcp.Tools;
using Moq;
using System.Text.Json;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Handler tests of the <c>fm_securities_analysis</c> opt-in <c>includeEurValue</c>
/// contract: per-position <c>valueEur</c>/<c>fxRate</c> at the stored price date
/// (division by "foreign currency per 1 EUR"), EUR factor-1 shortcut, null semantics for
/// missing prices/rates and the statistics fields <c>marketValueEur</c>/
/// <c>totalMarketValueEur</c> that only exist when both flags are set.
/// </summary>
public sealed class FmSecuritiesAnalysisEurValueTests : McpToolTestBase
{
    private static SecurityDto NewSecurityDto(string currency = "EUR", decimal? openQuantity = null, bool isActive = true)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Identifier = "ISIN",
            CurrencyCode = currency,
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

    private static Mock<IFxRateService> FxReturning(params (string Currency, DateTime Date, FxRate Rate)[] rows)
    {
        var fx = new Mock<IFxRateService>();
        fx.Setup(f => f.GetRateAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string cc, DateTime d, CancellationToken _) =>
                rows.FirstOrDefault(r => string.Equals(r.Currency, cc, StringComparison.OrdinalIgnoreCase) && r.Date == d.Date).Rate);
        return fx;
    }

    /// <summary>
    /// An EUR position converts 1:1 — <c>valueEur = openQuantity x latestPrice.close</c>
    /// and <c>fxRate</c> stays <c>null</c> because no conversion happened.
    /// </summary>
    [Fact]
    public async Task Invoke_EurPosition_ConvertsOneToOne_WithoutFxRate()
    {
        var owner = Guid.NewGuid();
        var security = NewSecurityDto(currency: "EUR", openQuantity: 10m);
        var priceDate = new DateTime(2026, 11, 20);

        var fx = new Mock<IFxRateService>(MockBehavior.Strict);
        var handler = NewHandler(
            SecuritiesReturning(owner, security).Object,
            PricesReturning(owner, (security.Id, 123.50m, priceDate)).Object,
            NewFilter(), fx.Object);
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true,\"includeEurValue\":true}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var entry = doc.RootElement.GetProperty("securities")[0];
        Assert.Equal(1235m, entry.GetProperty("valueEur").GetDecimal());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("fxRate").ValueKind);
    }

    /// <summary>
    /// A foreign-currency position divides by the stored FX rate at the price date:
    /// <c>valueEur = openQuantity x close / ratePerEur</c> (foreign currency per 1 EUR),
    /// and <c>fxRate {rate, date, source}</c> documents the used rate.
    /// </summary>
    [Fact]
    public async Task Invoke_ForeignCurrency_DividesByRatePerEur_AndEmitsFxRate()
    {
        var owner = Guid.NewGuid();
        var security = NewSecurityDto(currency: "USD", openQuantity: 10m);
        var priceDate = new DateTime(2026, 11, 20);
        var rateDate = new DateTime(2026, 11, 20);
        var rate = new FxRate(rateDate, 1.09m, "Frankfurter");

        var handler = NewHandler(
            SecuritiesReturning(owner, security).Object,
            PricesReturning(owner, (security.Id, 109m, priceDate)).Object,
            NewFilter(),
            FxReturning(("USD", priceDate, rate)).Object);
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true,\"includeEurValue\":true}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var entry = doc.RootElement.GetProperty("securities")[0];
        // 10 x 109 USD / 1.09 USD-per-EUR = 1000 EUR — a multiplication here would be wrong.
        Assert.Equal(1000m, entry.GetProperty("valueEur").GetDecimal());
        var fxRate = entry.GetProperty("fxRate");
        Assert.Equal(1.09m, fxRate.GetProperty("rate").GetDecimal());
        Assert.Equal(rateDate, fxRate.GetProperty("date").GetDateTime());
        Assert.Equal("Frankfurter", fxRate.GetProperty("source").GetString());
        Assert.True(fxRate.GetProperty("date").GetDateTime() <= priceDate);
    }

    /// <summary>
    /// Without a stored price there is no conversion basis: <c>valueEur</c> and
    /// <c>fxRate</c> are <c>null</c> and no FX lookup is issued at all.
    /// </summary>
    [Fact]
    public async Task Invoke_MissingPrice_NullsEurFields_AndSkipsFxLookup()
    {
        var owner = Guid.NewGuid();
        var security = NewSecurityDto(currency: "USD", openQuantity: 10m);

        var fx = new Mock<IFxRateService>(MockBehavior.Strict);
        var handler = NewHandler(
            SecuritiesReturning(owner, security).Object,
            PricesReturning(owner).Object,
            NewFilter(), fx.Object);
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true,\"includeEurValue\":true}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var entry = doc.RootElement.GetProperty("securities")[0];
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("valueEur").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("fxRate").ValueKind);
        fx.Verify(f => f.GetRateAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// When no FX rate can be resolved for the price date the position keeps
    /// <c>valueEur: null</c> and <c>fxRate: null</c> — never a made-up conversion.
    /// </summary>
    [Fact]
    public async Task Invoke_MissingFxRate_NullsEurFields()
    {
        var owner = Guid.NewGuid();
        var security = NewSecurityDto(currency: "USD", openQuantity: 10m);
        var priceDate = new DateTime(2026, 11, 20);

        var handler = NewHandler(
            SecuritiesReturning(owner, security).Object,
            PricesReturning(owner, (security.Id, 109m, priceDate)).Object,
            NewFilter(),
            FxReturning().Object);
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true,\"includeEurValue\":true}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var entry = doc.RootElement.GetProperty("securities")[0];
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("valueEur").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("fxRate").ValueKind);
    }

    /// <summary>
    /// Without <c>includeEurValue</c> the EUR fields are emitted as <c>null</c> and the FX
    /// service is never touched.
    /// </summary>
    [Fact]
    public async Task Invoke_WithoutIncludeEurValue_NullsEurFields_AndSkipsFx()
    {
        var owner = Guid.NewGuid();
        var security = NewSecurityDto(currency: "USD", openQuantity: 10m);
        var priceDate = new DateTime(2026, 11, 20);

        var fx = new Mock<IFxRateService>(MockBehavior.Strict);
        var handler = NewHandler(
            SecuritiesReturning(owner, security).Object,
            PricesReturning(owner, (security.Id, 109m, priceDate)).Object,
            NewFilter(), fx.Object);
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var entry = doc.RootElement.GetProperty("securities")[0];
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("valueEur").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("fxRate").ValueKind);
        fx.Verify(f => f.GetRateAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Positions sharing currency and price date issue a single FX service call — the
    /// handler pre-groups the lookup pairs instead of calling per position.
    /// </summary>
    [Fact]
    public async Task Invoke_SharedCurrencyAndDate_ResolvesFxRateOnce()
    {
        var owner = Guid.NewGuid();
        var first = NewSecurityDto(currency: "USD", openQuantity: 10m);
        var second = NewSecurityDto(currency: "USD", openQuantity: 5m);
        var priceDate = new DateTime(2026, 11, 20);
        var rate = new FxRate(priceDate, 1.09m, "Frankfurter");

        var fx = FxReturning(("USD", priceDate, rate));
        var handler = NewHandler(
            SecuritiesReturning(owner, first, second).Object,
            PricesReturning(owner, (first.Id, 109m, priceDate), (second.Id, 54.5m, priceDate)).Object,
            NewFilter(), fx.Object);
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true,\"includeEurValue\":true}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        fx.Verify(f => f.GetRateAsync("USD", priceDate, It.IsAny<CancellationToken>()), Times.Once);
        var entries = doc.RootElement.GetProperty("securities");
        Assert.Equal(1000m, entries[0].GetProperty("valueEur").GetDecimal());
        Assert.Equal(250m, entries[1].GetProperty("valueEur").GetDecimal());
    }

    /// <summary>
    /// With both flags each <c>byCurrency</c> row gains <c>marketValueEur</c> and the
    /// statistics gains <c>totalMarketValueEur</c> — converted sums over the valued
    /// positions, EUR rows convert 1:1.
    /// </summary>
    [Fact]
    public async Task Invoke_Statistics_EmitsEurAggregates_WhenBothFlagsSet()
    {
        var owner = Guid.NewGuid();
        var eur = NewSecurityDto(currency: "EUR", openQuantity: 10m);
        var usd = NewSecurityDto(currency: "USD", openQuantity: 10m);
        var priceDate = new DateTime(2026, 11, 20);

        var handler = NewHandler(
            SecuritiesReturning(owner, eur, usd).Object,
            PricesReturning(owner, (eur.Id, 100m, priceDate), (usd.Id, 109m, priceDate)).Object,
            NewFilter(),
            FxReturning(("USD", priceDate, new FxRate(priceDate, 1.09m, "Frankfurter"))).Object);
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true,\"includeStatistics\":true,\"includeEurValue\":true}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var stats = doc.RootElement.GetProperty("statistics");
        Assert.Equal(2000m, stats.GetProperty("totalMarketValueEur").GetDecimal());
        var rows = stats.GetProperty("byCurrency");
        var eurRow = rows.EnumerateArray().Single(r => r.GetProperty("currencyCode").GetString() == "EUR");
        var usdRow = rows.EnumerateArray().Single(r => r.GetProperty("currencyCode").GetString() == "USD");
        Assert.Equal(1000m, eurRow.GetProperty("marketValueEur").GetDecimal());
        Assert.Equal(1000m, usdRow.GetProperty("marketValueEur").GetDecimal());
    }

    /// <summary>
    /// A currency row whose valued position lacks an FX rate reports
    /// <c>marketValueEur: null</c>, and an incomplete conversion also nulls
    /// <c>totalMarketValueEur</c> — no silently partial sums.
    /// </summary>
    [Fact]
    public async Task Invoke_Statistics_NullsEurAggregates_WhenConversionIncomplete()
    {
        var owner = Guid.NewGuid();
        var eur = NewSecurityDto(currency: "EUR", openQuantity: 10m);
        var usd = NewSecurityDto(currency: "USD", openQuantity: 10m);
        var priceDate = new DateTime(2026, 11, 20);

        var handler = NewHandler(
            SecuritiesReturning(owner, eur, usd).Object,
            PricesReturning(owner, (eur.Id, 100m, priceDate), (usd.Id, 109m, priceDate)).Object,
            NewFilter(),
            FxReturning().Object); // no USD rate resolvable
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true,\"includeStatistics\":true,\"includeEurValue\":true}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var stats = doc.RootElement.GetProperty("statistics");
        Assert.Equal(JsonValueKind.Null, stats.GetProperty("totalMarketValueEur").ValueKind);
        var rows = stats.GetProperty("byCurrency");
        var eurRow = rows.EnumerateArray().Single(r => r.GetProperty("currencyCode").GetString() == "EUR");
        var usdRow = rows.EnumerateArray().Single(r => r.GetProperty("currencyCode").GetString() == "USD");
        Assert.Equal(1000m, eurRow.GetProperty("marketValueEur").GetDecimal());
        Assert.Equal(JsonValueKind.Null, usdRow.GetProperty("marketValueEur").ValueKind);
    }

    /// <summary>
    /// Without <c>includeEurValue</c> the statistics carries no EUR fields at all —
    /// <c>marketValueEur</c>/<c>totalMarketValueEur</c> exist only when both flags are set.
    /// </summary>
    [Fact]
    public async Task Invoke_Statistics_WithoutIncludeEurValue_OmitsEurFields()
    {
        var owner = Guid.NewGuid();
        var usd = NewSecurityDto(currency: "USD", openQuantity: 10m);
        var priceDate = new DateTime(2026, 11, 20);

        var fx = new Mock<IFxRateService>(MockBehavior.Strict);
        var handler = NewHandler(
            SecuritiesReturning(owner, usd).Object,
            PricesReturning(owner, (usd.Id, 109m, priceDate)).Object,
            NewFilter(), fx.Object);
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true,\"includeStatistics\":true}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var stats = doc.RootElement.GetProperty("statistics");
        Assert.False(stats.TryGetProperty("totalMarketValueEur", out _));
        Assert.False(stats.GetProperty("byCurrency")[0].TryGetProperty("marketValueEur", out _));
    }
}

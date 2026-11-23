using FinanceManager.Application.Mcp;
using FinanceManager.Application.Securities;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.Mcp;
using FinanceManager.Web.Mcp.Tools;
using Moq;
using System.Text.Json;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Handler tests of the <c>fm_securities_analysis</c> <c>latestPrice</c> output field: the
/// always-on last stored close price (incl. date) per emitted security — in the list and the
/// single-security path, independent of <c>includePrices</c>/<c>priceTake</c>, independent of
/// <c>hasPriceError</c>, and only looked up for record-filter-visible securities.
/// </summary>
public sealed class FmSecuritiesAnalysisToolTests : McpToolTestBase
{
    private static SecurityDto NewSecurityDto(Guid? categoryId = null, bool hasPriceError = false)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = "Test ETF",
            Identifier = "ISIN123",
            CurrencyCode = "EUR",
            CategoryId = categoryId,
            IsActive = true,
            CreatedUtc = DateTime.UtcNow,
            HasPriceError = hasPriceError
        };

    private static FmSecuritiesAnalysisHandler NewHandler(
        ISecurityService securities, ISecurityPriceService prices, McpRecordScopeFilter filter,
        IFxRateService? fxRates = null)
        => new(securities, prices, Mock.Of<ISecurityCategoryService>(), filter, fxRates ?? Mock.Of<IFxRateService>());

    /// <summary>
    /// The list path emits <c>latestPrice</c> even when <c>includePrices</c> is not set —
    /// <c>prices</c> stays <c>null</c> while <c>latestPrice</c> carries close and date of the
    /// batch result.
    /// </summary>
    [Fact]
    public async Task Invoke_ListPath_IncludesLatestPrice_WithoutIncludePrices()
    {
        var owner = Guid.NewGuid();
        var security = NewSecurityDto();
        var latestDate = new DateTime(2026, 11, 18);

        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.ListAsync(owner, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { security });
        var prices = new Mock<ISecurityPriceService>();
        prices.Setup(p => p.GetLatestPricesAsync(owner,
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(security.Id)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SecurityPriceDto>
            {
                [security.Id] = new(latestDate, 123.45m)
            });

        var handler = NewHandler(securities.Object, prices.Object, NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var entry = doc.RootElement.GetProperty("securities")[0];
        var latest = entry.GetProperty("latestPrice");
        Assert.Equal(JsonValueKind.Object, latest.ValueKind);
        Assert.Equal(123.45m, latest.GetProperty("close").GetDecimal());
        Assert.Equal(latestDate, latest.GetProperty("date").GetDateTime());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("prices").ValueKind);
    }

    /// <summary>
    /// A security without any stored price renders <c>latestPrice: null</c>.
    /// </summary>
    [Fact]
    public async Task Invoke_LatestPrice_Null_WhenNoStoredPrice()
    {
        var owner = Guid.NewGuid();
        var security = NewSecurityDto();

        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.ListAsync(owner, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { security });
        var prices = new Mock<ISecurityPriceService>();
        prices.Setup(p => p.GetLatestPricesAsync(owner, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SecurityPriceDto>());

        var handler = NewHandler(securities.Object, prices.Object, NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var entry = doc.RootElement.GetProperty("securities")[0];
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("latestPrice").ValueKind);
    }

    /// <summary>
    /// With several stored dates <c>latestPrice</c> surfaces the most recent stored price —
    /// the entry the batch lookup resolved, matching the newest entry of the price history.
    /// </summary>
    [Fact]
    public async Task Invoke_LatestPrice_SelectsMostRecentDate()
    {
        var owner = Guid.NewGuid();
        var security = NewSecurityDto();
        var stored = new[]
        {
            new SecurityPriceDto(new DateTime(2026, 11, 10), 100m),
            new SecurityPriceDto(new DateTime(2026, 11, 19), 145.60m),
            new SecurityPriceDto(new DateTime(2026, 11, 12), 120m)
        };
        var newest = stored.MaxBy(p => p.Date)!;

        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.ListAsync(owner, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { security });
        var prices = new Mock<ISecurityPriceService>();
        prices.Setup(p => p.GetLatestPricesAsync(owner, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SecurityPriceDto> { [security.Id] = newest });
        prices.Setup(p => p.ListAsync(owner, security.Id, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored.OrderByDescending(p => p.Date).ToList());

        var handler = NewHandler(securities.Object, prices.Object, NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true,\"includePrices\":true}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var entry = doc.RootElement.GetProperty("securities")[0];
        var latest = entry.GetProperty("latestPrice");
        Assert.Equal(newest.Close, latest.GetProperty("close").GetDecimal());
        Assert.Equal(newest.Date, latest.GetProperty("date").GetDateTime());
        // Consistency: the first history entry (descending order) is the same stored price.
        Assert.Equal(latest.GetProperty("date").GetString(), entry.GetProperty("prices")[0].GetProperty("date").GetString());
    }

    /// <summary>
    /// A stored price is still emitted in <c>latestPrice</c> when <c>hasPriceError</c> is set —
    /// the flag remains an independent signal.
    /// </summary>
    [Fact]
    public async Task Invoke_LatestPrice_ReturnedDespiteHasPriceError()
    {
        var owner = Guid.NewGuid();
        var security = NewSecurityDto(hasPriceError: true);

        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.ListAsync(owner, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { security });
        var prices = new Mock<ISecurityPriceService>();
        prices.Setup(p => p.GetLatestPricesAsync(owner, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SecurityPriceDto>
            {
                [security.Id] = new(new DateTime(2026, 11, 5), 88.20m)
            });

        var handler = NewHandler(securities.Object, prices.Object, NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var entry = doc.RootElement.GetProperty("securities")[0];
        Assert.True(entry.GetProperty("hasPriceError").GetBoolean());
        var latest = entry.GetProperty("latestPrice");
        Assert.Equal(JsonValueKind.Object, latest.ValueKind);
        Assert.Equal(88.20m, latest.GetProperty("close").GetDecimal());
    }

    /// <summary>
    /// The single-security path (<c>securityId</c>) emits <c>latestPrice</c> with the same
    /// schema, next to the default-on <c>prices</c> history.
    /// </summary>
    [Fact]
    public async Task Invoke_SinglePath_IncludesLatestPrice()
    {
        var owner = Guid.NewGuid();
        var security = NewSecurityDto();
        var latestDate = new DateTime(2026, 11, 18);

        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.GetAsync(security.Id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(security);
        var prices = new Mock<ISecurityPriceService>();
        prices.Setup(p => p.GetLatestPricesAsync(owner,
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(security.Id)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SecurityPriceDto>
            {
                [security.Id] = new(latestDate, 77.77m)
            });
        prices.Setup(p => p.ListAsync(owner, security.Id, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new SecurityPriceDto(latestDate, 77.77m) });

        var handler = NewHandler(securities.Object, prices.Object, NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"securityId\":\"{security.Id}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var entry = doc.RootElement.GetProperty("securities")[0];
        var latest = entry.GetProperty("latestPrice");
        Assert.Equal(JsonValueKind.Object, latest.ValueKind);
        Assert.Equal(77.77m, latest.GetProperty("close").GetDecimal());
        Assert.Equal(latestDate, latest.GetProperty("date").GetDateTime());
        Assert.Equal(JsonValueKind.Array, entry.GetProperty("prices").ValueKind);
    }

    /// <summary>
    /// The record scope filter runs before the price lookup: a category-filtered security
    /// is absent from the output and its id is never passed to the batch price query.
    /// </summary>
    [Fact]
    public async Task Invoke_RecordFilter_AppliesBeforePriceLookup()
    {
        var owner = Guid.NewGuid();
        var access = NewAccess(owner, McpAccessLevel.Individuell);
        var allowedCategory = Guid.NewGuid();
        var scope = new McpAccessScope(access.Id, owner, McpDataType.Securities, McpRightKind.Read,
            securityCategoryIdsCsv: allowedCategory.ToString());

        var visible = NewSecurityDto(categoryId: allowedCategory);
        var filteredOut = NewSecurityDto(categoryId: Guid.NewGuid());

        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.ListAsync(owner, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { visible, filteredOut });
        var prices = new Mock<ISecurityPriceService>();
        prices.Setup(p => p.GetLatestPricesAsync(owner, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SecurityPriceDto>());

        var handler = NewHandler(securities.Object, prices.Object, NewFilter(scope));
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true}"), NewCallContext(NewAccessContext(owner, access).Object), CancellationToken.None));

        var emitted = doc.RootElement.GetProperty("securities");
        Assert.Equal(1, emitted.GetArrayLength());
        Assert.Equal(visible.Id, emitted[0].GetProperty("id").GetGuid());

        prices.Verify(p => p.GetLatestPricesAsync(owner,
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(visible.Id) && !ids.Contains(filteredOut.Id)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

using FinanceManager.Domain.Postings;
using FinanceManager.Domain.Securities;
using FinanceManager.Infrastructure;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.Securities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Integration;

/// <summary>
/// Wire-level coverage of <c>fm_securities_analysis</c> over the real <c>/mcp</c> surface:
/// each emitted security carries the always-on <c>latestPrice { close, date }</c> of the last
/// stored close price — serialized with the same date format as the <c>prices[]</c> history.
/// </summary>
public sealed class McpSecuritiesAnalysisTests : McpIntegrationTestBase
{
    /// <summary>Initializes the suite with a dedicated factory where MCP is enabled.</summary>
    /// <param name="factory">The shared web application factory.</param>
    public McpSecuritiesAnalysisTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// A seeded security with two stored prices answers the single-security call with
    /// <c>latestPrice</c> carrying the newest stored close and date — the <c>date</c> value
    /// is serialized identically to the <c>prices[].date</c> history entries (the single
    /// path enables <c>includePrices</c> by default, allowing a direct wire comparison).
    /// </summary>
    [Fact]
    public async Task ToolsCall_SecuritiesAnalysis_ReturnsLatestPrice()
    {
        var api = CreateApiClient();
        var username = $"mcpsec_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null), TestContext.Current.CancellationToken);

        Guid userId;
        Guid securityId;
        var latestDate = new DateTime(2026, 11, 18);
        var earlierDate = new DateTime(2026, 11, 10);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            userId = await db.Users
                .Where(x => x.UserName == username)
                .Select(x => x.Id)
                .FirstAsync(TestContext.Current.CancellationToken);

            var security = new Security(userId, "Test ETF", "ISIN-SECLATEST", null, "ETF", "EUR", null);
            db.Securities.Add(security);
            db.SecurityPrices.Add(new SecurityPrice(security.Id, earlierDate, 100.00m));
            db.SecurityPrices.Add(new SecurityPrice(security.Id, latestDate, 123.45m));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            securityId = security.Id;
        }

        using var client = _factory.CreateClient();
        var pat = await CreateAccessTokenAsync(client, username, level: 0 /* Analyse */, approvalMode: 0);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);

        var result = await CallToolAsync(client, "fm_securities_analysis",
            new { securityId = securityId.ToString() }, id: 1);
        var securities = result.GetProperty("structuredContent").GetProperty("securities");
        securities.GetArrayLength().Should().Be(1);
        var entry = securities[0];

        var latest = entry.GetProperty("latestPrice");
        latest.GetProperty("close").GetDecimal().Should().Be(123.45m);
        latest.GetProperty("date").GetDateTime().Should().Be(latestDate);

        // Same serialization as the prices[] history (single path defaults includePrices on).
        var prices = entry.GetProperty("prices");
        prices.GetArrayLength().Should().Be(2);
        latest.GetProperty("date").GetString().Should().Be(prices[0].GetProperty("date").GetString());
    }

    /// <summary>
    /// A seeded security with a recorded price error answers the call with the failure
    /// timestamp as top-level <c>priceErrorSinceUtc</c> and the provider message inside
    /// <c>untrusted_data.priceErrorMessage</c> — the message is not duplicated at top level.
    /// </summary>
    [Fact]
    public async Task ToolsCall_SecuritiesAnalysis_ReturnsPriceErrorDetails()
    {
        var api = CreateApiClient();
        var username = $"mcpsecerr_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null), TestContext.Current.CancellationToken);

        Guid userId;
        Guid securityId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            userId = await db.Users
                .Where(x => x.UserName == username)
                .Select(x => x.Id)
                .FirstAsync(TestContext.Current.CancellationToken);

            var security = new Security(userId, "Broken Feed Corp", "ISIN-SECERR", null, null, "USD", null);
            security.SetPriceError("Symbol not found");
            db.Securities.Add(security);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            securityId = security.Id;
        }

        using var client = _factory.CreateClient();
        var pat = await CreateAccessTokenAsync(client, username, level: 0 /* Analyse */, approvalMode: 0);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);

        var result = await CallToolAsync(client, "fm_securities_analysis",
            new { securityId = securityId.ToString() }, id: 2);
        var entry = result.GetProperty("structuredContent").GetProperty("securities")[0];

        entry.GetProperty("hasPriceError").GetBoolean().Should().BeTrue();
        entry.GetProperty("priceErrorSinceUtc").GetDateTime().Should().BeAfter(DateTime.MinValue);
        entry.GetProperty("untrusted_data").GetProperty("priceErrorMessage").GetString()
            .Should().Be("Symbol not found");
        entry.TryGetProperty("priceErrorMessage", out _).Should().BeFalse();
    }

    /// <summary>
    /// With <c>includeStatistics: true</c> the wire response carries a <c>statistics</c>
    /// object: currency-pure <c>byCurrency</c> market values (openQuantity x latest stored
    /// close), a single-currency total, and <c>positionsWithoutPrice</c> for a holding that
    /// has no stored close.
    /// </summary>
    [Fact]
    public async Task ToolsCall_SecuritiesAnalysis_IncludeStatistics_ReturnsAggregate()
    {
        var api = CreateApiClient();
        var username = $"mcpsecstat_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null), TestContext.Current.CancellationToken);

        Guid userId;
        Guid unpricedId;
        var priceDate = new DateTime(2026, 11, 20);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            userId = await db.Users
                .Where(x => x.UserName == username)
                .Select(x => x.Id)
                .FirstAsync(TestContext.Current.CancellationToken);

            var priced = new Security(userId, "Priced ETF", "ISIN-STAT1", null, null, "EUR", null);
            var pricedUsd = new Security(userId, "Priced US", "ISIN-STAT3", null, null, "USD", null);
            var unpriced = new Security(userId, "Unpriced AG", "ISIN-STAT2", null, null, "USD", null);
            db.Securities.AddRange(priced, pricedUsd, unpriced);
            db.SecurityPrices.Add(new SecurityPrice(priced.Id, priceDate, 100.00m));
            db.SecurityPrices.Add(new SecurityPrice(pricedUsd.Id, priceDate, 50.00m));
            // OpenQuantity derives from buy/sell postings.
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, priced.Id,
                priceDate, priceDate, 1000m, null, null, null, SecurityPostingSubType.Buy, 10m));
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, pricedUsd.Id,
                priceDate, priceDate, 400m, null, null, null, SecurityPostingSubType.Buy, 8m));
            db.Postings.Add(new Posting(Guid.NewGuid(), PostingKind.Security, null, null, null, unpriced.Id,
                priceDate, priceDate, 500m, null, null, null, SecurityPostingSubType.Buy, 5m));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            unpricedId = unpriced.Id;
        }

        using var client = _factory.CreateClient();
        var pat = await CreateAccessTokenAsync(client, username, level: 0 /* Analyse */, approvalMode: 0);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);

        var result = await CallToolAsync(client, "fm_securities_analysis",
            new { includeStatistics = true }, id: 3);
        var stats = result.GetProperty("structuredContent").GetProperty("statistics");

        stats.GetProperty("securityCount").GetInt32().Should().Be(3);
        stats.GetProperty("positionCount").GetInt32().Should().Be(3);
        stats.GetProperty("positionsWithoutPrice").GetProperty("count").GetInt32().Should().Be(1);
        stats.GetProperty("positionsWithoutPrice").GetProperty("securityIds")[0].GetGuid().Should().Be(unpricedId);

        var byCurrency = stats.GetProperty("byCurrency");
        byCurrency.GetArrayLength().Should().Be(2);
        byCurrency.EnumerateArray().Single(r => r.GetProperty("currencyCode").GetString() == "EUR")
            .GetProperty("marketValue").GetDecimal().Should().Be(1000m);
        var usdRow = byCurrency.EnumerateArray().Single(r => r.GetProperty("currencyCode").GetString() == "USD");
        usdRow.GetProperty("marketValue").GetDecimal().Should().Be(400m);
        usdRow.GetProperty("positionsWithoutPrice").GetInt32().Should().Be(1);
        // Mixed valued currencies: no misleading cross-currency total.
        stats.GetProperty("totalMarketValue").ValueKind.Should().Be(JsonValueKind.Null);
        stats.GetProperty("totalCurrencyCode").ValueKind.Should().Be(JsonValueKind.Null);
    }
}

using FinanceManager.Domain.Securities;
using FinanceManager.Infrastructure;
using FinanceManager.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
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
}

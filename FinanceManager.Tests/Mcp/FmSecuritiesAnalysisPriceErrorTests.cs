using FinanceManager.Application.Securities;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.Mcp;
using FinanceManager.Web.Mcp.Tools;
using Moq;
using System.Text.Json;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Handler tests of the <c>fm_securities_analysis</c> price-error transparency fields:
/// <c>priceErrorSinceUtc</c> as structured top-level field and <c>priceErrorMessage</c> as
/// provider-originated text under <c>untrusted_data</c> — in the list and the
/// single-security path — plus descriptor documentation of the data semantics.
/// </summary>
public sealed class FmSecuritiesAnalysisPriceErrorTests : McpToolTestBase
{
    private static readonly DateTime ErrorSince = new(2026, 10, 1, 8, 30, 0, DateTimeKind.Utc);

    private static SecurityDto NewSecurityDto(bool hasPriceError = false, string? errorMessage = null, DateTime? errorSinceUtc = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = "Test ETF",
            Identifier = "ISIN123",
            CurrencyCode = "EUR",
            IsActive = true,
            CreatedUtc = DateTime.UtcNow,
            HasPriceError = hasPriceError,
            PriceErrorMessage = errorMessage,
            PriceErrorSinceUtc = errorSinceUtc
        };

    private static FmSecuritiesAnalysisHandler NewHandler(
        ISecurityService securities, ISecurityPriceService prices, McpRecordScopeFilter filter,
        IFxRateService? fxRates = null)
        => new(securities, prices, Mock.Of<ISecurityCategoryService>(), filter, fxRates ?? Mock.Of<IFxRateService>());

    private static Mock<ISecurityPriceService> EmptyPrices(Guid owner)
    {
        var prices = new Mock<ISecurityPriceService>();
        prices.Setup(p => p.GetLatestPricesAsync(owner, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SecurityPriceDto>());
        return prices;
    }

    /// <summary>
    /// A security with a recorded price error exposes the provider message under
    /// <c>untrusted_data.priceErrorMessage</c> and the failure timestamp as top-level
    /// <c>priceErrorSinceUtc</c> in the list path.
    /// </summary>
    [Fact]
    public async Task Invoke_ListPath_EmitsPriceErrorDetails_UnderUntrustedAndTopLevel()
    {
        var owner = Guid.NewGuid();
        var security = NewSecurityDto(hasPriceError: true, errorMessage: "Symbol not found", errorSinceUtc: ErrorSince);

        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.ListAsync(owner, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { security });

        var handler = NewHandler(securities.Object, EmptyPrices(owner).Object, NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var entry = doc.RootElement.GetProperty("securities")[0];
        Assert.True(entry.GetProperty("hasPriceError").GetBoolean());
        Assert.Equal(ErrorSince, entry.GetProperty("priceErrorSinceUtc").GetDateTime());
        var untrusted = entry.GetProperty("untrusted_data");
        Assert.Equal("Symbol not found", untrusted.GetProperty("priceErrorMessage").GetString());
        Assert.Throws<KeyNotFoundException>(() => entry.GetProperty("priceErrorMessage"));
    }

    /// <summary>
    /// A security without a price error serializes <c>priceErrorSinceUtc</c> and
    /// <c>untrusted_data.priceErrorMessage</c> as <c>null</c>.
    /// </summary>
    [Fact]
    public async Task Invoke_ListPath_NullErrorFields_WhenNoPriceError()
    {
        var owner = Guid.NewGuid();
        var security = NewSecurityDto();

        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.ListAsync(owner, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { security });

        var handler = NewHandler(securities.Object, EmptyPrices(owner).Object, NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args("{\"onlyActive\":true}"), NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var entry = doc.RootElement.GetProperty("securities")[0];
        Assert.False(entry.GetProperty("hasPriceError").GetBoolean());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("priceErrorSinceUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("untrusted_data").GetProperty("priceErrorMessage").ValueKind);
    }

    /// <summary>
    /// The single-security path (<c>securityId</c>) exposes the same error fields.
    /// </summary>
    [Fact]
    public async Task Invoke_SinglePath_EmitsPriceErrorDetails()
    {
        var owner = Guid.NewGuid();
        var security = NewSecurityDto(hasPriceError: true, errorMessage: "Rate limit exceeded", errorSinceUtc: ErrorSince);

        var securities = new Mock<ISecurityService>();
        securities.Setup(s => s.GetAsync(security.Id, owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(security);

        var prices = EmptyPrices(owner);
        prices.Setup(p => p.ListAsync(owner, security.Id, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecurityPriceDto>());

        var handler = NewHandler(securities.Object, prices.Object, NewFilter());
        using var doc = Serialize(await handler.InvokeAsync(
            Args($"{{\"securityId\":\"{security.Id}\"}}"),
            NewCallContext(NewAccessContext(owner).Object), CancellationToken.None));

        var entry = doc.RootElement.GetProperty("securities")[0];
        Assert.Equal(ErrorSince, entry.GetProperty("priceErrorSinceUtc").GetDateTime());
        Assert.Equal("Rate limit exceeded", entry.GetProperty("untrusted_data").GetProperty("priceErrorMessage").GetString());
    }

    /// <summary>
    /// The descriptor description documents the data semantics: <c>hasPriceError</c>
    /// lifecycle, empty <c>prices</c> causes and negative <c>openQuantity</c> meaning.
    /// </summary>
    [Fact]
    public void Descriptor_Description_DocumentsDataSemantics()
    {
        var description = new FmSecuritiesAnalysisDescriptor().Description;

        Assert.Contains("hasPriceError", description);
        Assert.Contains("priceErrorMessage", description);
        Assert.Contains("prices", description);
        Assert.Contains("AlphaVantageCode", description);
        Assert.Contains("openQuantity", description);
        Assert.Contains("short", description, StringComparison.OrdinalIgnoreCase);
    }
}

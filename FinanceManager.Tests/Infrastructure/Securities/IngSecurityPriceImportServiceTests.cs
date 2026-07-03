using System.Text;
using FinanceManager.Application.Securities;
using FinanceManager.Infrastructure.Securities;
using FinanceManager.Shared.Dtos.Securities;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Infrastructure.Securities;

public sealed class IngSecurityPriceImportServiceTests
{
    /// <summary>
    /// Verifies that provider matching handles casing and surrounding whitespace.
    /// </summary>
    [Theory]
    [InlineData("ing")]
    [InlineData(" ING ")]
    [InlineData("InG")]
    public void CanHandle_ShouldReturnTrue_WhenProviderIsIngIgnoringCaseAndWhitespace(string provider)
    {
        var sut = new IngSecurityPriceImportService(Mock.Of<ISecurityPriceService>(), Mock.Of<ILogger<IngSecurityPriceImportService>>());
        var context = new SecurityPriceImportContext(provider, "prices.txt", "text/plain");

        var canHandle = sut.CanHandle(context);

        Assert.True(canHandle);
    }

    /// <summary>
    /// Verifies that missing provider hint is rejected.
    /// </summary>
    [Fact]
    public void CanHandle_ShouldReturnFalse_WhenProviderMissing()
    {
        var sut = new IngSecurityPriceImportService(Mock.Of<ISecurityPriceService>(), Mock.Of<ILogger<IngSecurityPriceImportService>>());
        var context = new SecurityPriceImportContext(null, "prices.csv", "text/csv");

        var canHandle = sut.CanHandle(context);

        Assert.False(canHandle);
    }

    /// <summary>
    /// Verifies that unsupported provider and non-csv files are rejected.
    /// </summary>
    [Fact]
    public void CanHandle_ShouldReturnFalse_WhenProviderNotIngAndFileIsNotCsv()
    {
        var sut = new IngSecurityPriceImportService(Mock.Of<ISecurityPriceService>(), Mock.Of<ILogger<IngSecurityPriceImportService>>());
        var context = new SecurityPriceImportContext("other", "prices.txt", "text/plain");

        var canHandle = sut.CanHandle(context);

        Assert.False(canHandle);
    }

    /// <summary>
    /// Verifies that valid ING CSV rows are parsed and forwarded to the upsert service.
    /// </summary>
    [Fact]
    public async Task ImportAsync_ShouldParseAndUpsert_WhenCsvIsValid()
    {
        var ownerUserId = Guid.NewGuid();
        var securityId = Guid.NewGuid();

        var priceService = new Mock<ISecurityPriceService>();
        priceService
            .Setup(x => x.UpsertDailyPricesAsync(ownerUserId, securityId, It.IsAny<IReadOnlyList<SecurityPriceImportItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityPriceImportResultDto(2, 0, 0, 0, Array.Empty<SecurityPriceImportErrorDto>()));

        var sut = new IngSecurityPriceImportService(priceService.Object, Mock.Of<ILogger<IngSecurityPriceImportService>>());
        var context = new SecurityPriceImportContext("ing", "prices.csv", "text/csv");
        var content = "sep=;\nZeit;Test Security\n01.07.2026 02:00:00;42,61\n02.07.2026 02:00:00;42,48\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await sut.ImportAsync(ownerUserId, securityId, stream, context, CancellationToken.None);

        Assert.Equal(2, result.Inserted);
        Assert.Empty(result.Errors);
        priceService.Verify(x => x.UpsertDailyPricesAsync(
            ownerUserId,
            securityId,
            It.Is<IReadOnlyList<SecurityPriceImportItem>>(items =>
                items.Count == 2 &&
                items[0].Date == new DateTime(2026, 7, 1) &&
                items[0].Close == 42.61m &&
                items[1].Date == new DateTime(2026, 7, 2) &&
                items[1].Close == 42.48m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that invalid rows reject the full file import.
    /// </summary>
    [Fact]
    public async Task ImportAsync_ShouldRejectImport_WhenCsvContainsInvalidLines()
    {
        var ownerUserId = Guid.NewGuid();
        var securityId = Guid.NewGuid();

        var priceService = new Mock<ISecurityPriceService>();
        priceService
            .Setup(x => x.UpsertDailyPricesAsync(ownerUserId, securityId, It.IsAny<IReadOnlyList<SecurityPriceImportItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityPriceImportResultDto(1, 0, 0, 0, Array.Empty<SecurityPriceImportErrorDto>()));

        var sut = new IngSecurityPriceImportService(priceService.Object, Mock.Of<ILogger<IngSecurityPriceImportService>>());
        var context = new SecurityPriceImportContext("ing", "prices.csv", "text/csv");
        var content = "sep=;\nZeit;Test Security\n01.07.2026 02:00:00;42,61\nnot-a-date;42,22\n02.07.2026 02:00:00;invalid\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await sut.ImportAsync(ownerUserId, securityId, stream, context, CancellationToken.None);

        Assert.Equal(0, result.Inserted);
        Assert.Equal(0, result.Skipped);
        Assert.Single(result.Errors);
        priceService.Verify(x => x.UpsertDailyPricesAsync(
            ownerUserId,
            securityId,
            It.IsAny<IReadOnlyList<SecurityPriceImportItem>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that rows with missing columns reject the full file import.
    /// </summary>
    [Fact]
    public async Task ImportAsync_ShouldRejectImport_WhenColumnsAreMissing()
    {
        var ownerUserId = Guid.NewGuid();
        var securityId = Guid.NewGuid();

        var priceService = new Mock<ISecurityPriceService>();
        priceService
            .Setup(x => x.UpsertDailyPricesAsync(ownerUserId, securityId, It.IsAny<IReadOnlyList<SecurityPriceImportItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityPriceImportResultDto(1, 0, 0, 0, Array.Empty<SecurityPriceImportErrorDto>()));

        var sut = new IngSecurityPriceImportService(priceService.Object, Mock.Of<ILogger<IngSecurityPriceImportService>>());
        var context = new SecurityPriceImportContext("ing", "prices.csv", "text/csv");
        var content = "sep=;\nZeit;Test Security\n01.07.2026 02:00:00;42,61\n02.07.2026 02:00:00\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await sut.ImportAsync(ownerUserId, securityId, stream, context, CancellationToken.None);

        Assert.Equal(0, result.Inserted);
        Assert.Equal(0, result.Skipped);
        Assert.Single(result.Errors);
        Assert.Equal("Invalid data row. Expected exactly two columns.", result.Errors[0].Message);
        priceService.Verify(x => x.UpsertDailyPricesAsync(
            ownerUserId,
            securityId,
            It.IsAny<IReadOnlyList<SecurityPriceImportItem>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that rows with negative close values reject the full file import.
    /// </summary>
    [Fact]
    public async Task ImportAsync_ShouldRejectImport_WhenCloseIsNegative()
    {
        var ownerUserId = Guid.NewGuid();
        var securityId = Guid.NewGuid();

        var priceService = new Mock<ISecurityPriceService>();
        priceService
            .Setup(x => x.UpsertDailyPricesAsync(ownerUserId, securityId, It.IsAny<IReadOnlyList<SecurityPriceImportItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityPriceImportResultDto(1, 0, 0, 0, Array.Empty<SecurityPriceImportErrorDto>()));

        var sut = new IngSecurityPriceImportService(priceService.Object, Mock.Of<ILogger<IngSecurityPriceImportService>>());
        var context = new SecurityPriceImportContext("ing", "prices.csv", "text/csv");
        var content = "sep=;\nZeit;Test Security\n01.07.2026 02:00:00;42,61\n02.07.2026 02:00:00;-10,00\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await sut.ImportAsync(ownerUserId, securityId, stream, context, CancellationToken.None);

        Assert.Equal(0, result.Inserted);
        Assert.Equal(0, result.Skipped);
        Assert.Single(result.Errors);
        Assert.Equal("Close value must not be negative.", result.Errors[0].Message);
        priceService.Verify(x => x.UpsertDailyPricesAsync(
            ownerUserId,
            securityId,
            It.IsAny<IReadOnlyList<SecurityPriceImportItem>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that files with mixed valid and invalid rows are rejected.
    /// </summary>
    [Fact]
    public async Task ImportAsync_ShouldRejectImport_WhenMixedValidAndInvalidRowsExist()
    {
        var ownerUserId = Guid.NewGuid();
        var securityId = Guid.NewGuid();

        var priceService = new Mock<ISecurityPriceService>();
        priceService
            .Setup(x => x.UpsertDailyPricesAsync(ownerUserId, securityId, It.IsAny<IReadOnlyList<SecurityPriceImportItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityPriceImportResultDto(2, 0, 0, 0, Array.Empty<SecurityPriceImportErrorDto>()));

        var sut = new IngSecurityPriceImportService(priceService.Object, Mock.Of<ILogger<IngSecurityPriceImportService>>());
        var context = new SecurityPriceImportContext("ing", "prices.csv", "text/csv");
        var content = "sep=;\nZeit;Test Security\n01.07.2026 02:00:00;42,61\nmissing-column\n02.07.2026 02:00:00;-10,00\n03.07.2026 02:00:00;44,00\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await sut.ImportAsync(ownerUserId, securityId, stream, context, CancellationToken.None);

        Assert.Equal(0, result.Inserted);
        Assert.Equal(0, result.Skipped);
        Assert.Single(result.Errors);
        priceService.Verify(x => x.UpsertDailyPricesAsync(
            ownerUserId,
            securityId,
            It.IsAny<IReadOnlyList<SecurityPriceImportItem>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}

using FinanceManager.Application;
using FinanceManager.Application.Portfolio;
using FinanceManager.Domain.Portfolio;
using FinanceManager.Shared.Dtos.Portfolio;
using FinanceManager.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FinanceManager.Tests.Controllers;

/// <summary>
/// Tests for <see cref="PortfolioAnalysisReportController"/> covering the analysis-report and
/// kpi-configuration endpoints.
/// </summary>
public sealed class PortfolioAnalysisReportControllerTests
{
    private static (PortfolioAnalysisReportController controller, Mock<IPortfolioAnalysisReportCacheService> cache, Mock<IPortfolioKpiConfigurationRepository> repo, Guid userId) Create()
    {
        var cache = new Mock<IPortfolioAnalysisReportCacheService>();
        var repo = new Mock<IPortfolioKpiConfigurationRepository>();
        var userId = Guid.NewGuid();
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(c => c.UserId).Returns(userId);

        var controller = new PortfolioAnalysisReportController(cache.Object, repo.Object, current.Object);
        return (controller, cache, repo, userId);
    }

    private static PortfolioAnalysisReportDto CreateReport()
        => new(
            new PortfolioStructureDto(1000m, 800m, 200m, [], [], [], [], [], []),
            new PortfolioPerformanceDto(null, null, [], []),
            new PortfolioCashflowDto(0m, 0m, 0m, 0m, 0m, 0m),
            new PortfolioRiskDto(null, null, null, null, null),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(10));

    /// <summary>
    /// Verifies that a successful report fetch from the cache service returns 200 with the report DTO.
    /// </summary>
    [Fact]
    public async Task GetAnalysisReport_Controller_Returns200AndData()
    {
        var (controller, cache, _, userId) = Create();
        cache.Setup(c => c.GetPortfolioReportAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReport());

        var result = await controller.GetAnalysisReportAsync(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<PortfolioAnalysisReportDto>();
    }

    /// <summary>
    /// Verifies that saving a KPI tile configuration persists it via the repository and invalidates the
    /// cached report, since the report's rendered tile set depends on this configuration.
    /// </summary>
    [Fact]
    public async Task PostKpiConfiguration_Controller_SavesAndReturns200()
    {
        var (controller, cache, repo, userId) = Create();
        var request = new PortfolioKpiConfigurationRequest
        {
            ActiveTileIds = [PortfolioTileId.Structure],
            TileOrder = [PortfolioTileId.Structure]
        };
        repo.Setup(r => r.UpsertAsync(userId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioKpiConfiguration(userId, "[0]", "[0]"));

        var result = await controller.SaveKpiConfigurationAsync(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        cache.Verify(c => c.InvalidateCacheAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that a KPI configuration with no active tiles is rejected as a validation problem, since a
    /// dashboard with zero configured tiles would leave the user with an empty report.
    /// </summary>
    [Fact]
    public async Task PostKpiConfiguration_Controller_RejectsEmptyActiveTiles()
    {
        var (controller, _, _, _) = Create();
        var request = new PortfolioKpiConfigurationRequest
        {
            ActiveTileIds = [],
            TileOrder = []
        };

        var result = await controller.SaveKpiConfigurationAsync(request, CancellationToken.None);

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    /// <summary>
    /// Verifies that the reset-cache endpoint invalidates the current user's cached report and returns 204.
    /// </summary>
    [Fact]
    public async Task ResetCache_Controller_InvalidatesCache()
    {
        var (controller, cache, _, userId) = Create();

        var result = await controller.ResetCacheAsync(CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        cache.Verify(c => c.InvalidateCacheAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}

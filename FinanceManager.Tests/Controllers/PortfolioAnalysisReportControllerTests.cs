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
            new PortfolioDepotDevelopmentDto([]),
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

    /// <summary>
    /// Verifies that the default configuration (no saved configuration) contains the new
    /// DepotDevelopment tile in both the active set and the tile order.
    /// </summary>
    [Fact]
    public async Task GetKpiConfiguration_Default_IncludesDepotDevelopment()
    {
        var (controller, _, repo, _) = Create();
        repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PortfolioKpiConfiguration?)null);

        var result = await controller.GetKpiConfigurationAsync(CancellationToken.None);

        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PortfolioKpiConfigurationDto>().Subject;
        dto.ActiveTileIds.Should().Contain(PortfolioTileId.DepotDevelopment);
        dto.TileOrder.Should().Contain(PortfolioTileId.DepotDevelopment);
    }

    /// <summary>
    /// Verifies that the default configuration (no saved configuration) is merged the same way as a
    /// saved one: tiles outside the default-active set (e.g. Risk) appear in the tile order so the
    /// edit list is identical for every user, without being activated.
    /// </summary>
    [Fact]
    public async Task GetKpiConfiguration_Default_MergesMissingTilesIntoOrder()
    {
        var (controller, _, repo, _) = Create();
        repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PortfolioKpiConfiguration?)null);

        var result = await controller.GetKpiConfigurationAsync(CancellationToken.None);

        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PortfolioKpiConfigurationDto>().Subject;
        dto.TileOrder.Should().Contain(PortfolioTileId.Risk);
        dto.ActiveTileIds.Should().NotContain(PortfolioTileId.Risk);
    }

    /// <summary>
    /// Verifies that a configuration saved before the DepotDevelopment tile existed gets the new tile
    /// appended to the tile order and activated, so existing users see it automatically.
    /// </summary>
    [Fact]
    public async Task GetKpiConfiguration_SavedConfigMissingTile_MergesIntoOrderAndActive()
    {
        var (controller, _, repo, userId) = Create();
        repo.Setup(r => r.GetAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioKpiConfiguration(userId, "[0,1,2]", "[0,1,2]"));

        var result = await controller.GetKpiConfigurationAsync(CancellationToken.None);

        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PortfolioKpiConfigurationDto>().Subject;
        dto.TileOrder.Should().Contain(PortfolioTileId.DepotDevelopment);
        dto.TileOrder[^1].Should().Be(PortfolioTileId.DepotDevelopment);
        dto.ActiveTileIds.Should().Contain(PortfolioTileId.DepotDevelopment);
    }

    /// <summary>
    /// Verifies that a tile which is already present in the stored tile order but not in the active
    /// set stays inactive after the merge — an explicit user deactivation is preserved.
    /// </summary>
    [Fact]
    public async Task GetKpiConfiguration_SavedConfigWithDeactivatedTile_StaysInactive()
    {
        var (controller, _, repo, userId) = Create();
        repo.Setup(r => r.GetAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioKpiConfiguration(userId, "[0,1,2]", "[0,1,2,4]"));

        var result = await controller.GetKpiConfigurationAsync(CancellationToken.None);

        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PortfolioKpiConfigurationDto>().Subject;
        dto.TileOrder.Should().Contain(PortfolioTileId.DepotDevelopment);
        dto.ActiveTileIds.Should().NotContain(PortfolioTileId.DepotDevelopment);
    }

    /// <summary>
    /// Verifies that tiles missing from a saved configuration that are not part of the default-active
    /// set (e.g. Risk) are appended to the tile order without being activated.
    /// </summary>
    [Fact]
    public async Task GetKpiConfiguration_SavedConfigMissingRisk_MergesIntoOrderOnly()
    {
        var (controller, _, repo, userId) = Create();
        repo.Setup(r => r.GetAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioKpiConfiguration(userId, "[0,1,2,4]", "[0,1,2,4]"));

        var result = await controller.GetKpiConfigurationAsync(CancellationToken.None);

        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PortfolioKpiConfigurationDto>().Subject;
        dto.TileOrder.Should().Contain(PortfolioTileId.Risk);
        dto.ActiveTileIds.Should().NotContain(PortfolioTileId.Risk);
    }
}

using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Portfolio;
using FinanceManager.Web.ViewModels.Portfolio;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Web.ViewModels.Portfolio;

/// <summary>
/// Tests for <see cref="PortfolioAnalysisReportPageViewModel"/> covering loading, edit mode,
/// saving configuration and refreshing the report.
/// </summary>
public sealed class PortfolioAnalysisReportPageViewModelTests
{
    private static PortfolioAnalysisReportDto CreateReport(decimal marketValue)
        => new(
            new PortfolioStructureDto(marketValue, 0m, marketValue, [], [], [], []),
            new PortfolioPerformanceDto(null, null, [], []),
            new PortfolioCashflowDto(0m, 0m, 0m, 0m),
            new PortfolioRiskDto(null, null, null, null, null),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(5));

    private static PortfolioKpiConfigurationDto CreateConfig()
        => new(
            [PortfolioTileId.Structure],
            [PortfolioTileId.Structure],
            DateTime.UtcNow);

    private static PortfolioAnalysisReportPageViewModel CreateVm(out Mock<IApiClient> apiMock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));
        return new PortfolioAnalysisReportPageViewModel(services.BuildServiceProvider());
    }

    [Fact]
    public async Task LoadReport_ViewModel_CallsServiceAndSetsData()
    {
        var vm = CreateVm(out var apiMock);
        apiMock.Setup(a => a.Portfolio_GetAnalysisReportAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReport(1234m));
        apiMock.Setup(a => a.Portfolio_GetKpiConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfig());

        await vm.LoadReportAsync();

        vm.PortfolioReportData.Should().NotBeNull();
        vm.PortfolioReportData!.Structure.TotalMarketValue.Should().Be(1234m);
        vm.CurrentConfiguration.Should().NotBeNull();
        apiMock.Verify(a => a.Portfolio_GetAnalysisReportAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EditMode_SaveConfiguration_PersistsAndInvalidatesCache()
    {
        var vm = CreateVm(out var apiMock);
        apiMock.Setup(a => a.Portfolio_GetKpiConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfig());
        apiMock.Setup(a => a.Portfolio_SaveKpiConfigurationAsync(It.IsAny<PortfolioKpiConfigurationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfig());
        apiMock.Setup(a => a.Portfolio_GetAnalysisReportAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReport(500m));

        await vm.EnterEditModeAsync();
        vm.IsEditMode.Should().BeTrue();

        var request = new PortfolioKpiConfigurationRequest
        {
            ActiveTileIds = [PortfolioTileId.Structure],
            TileOrder = [PortfolioTileId.Structure]
        };
        await vm.SaveConfigurationAsync(request);

        vm.IsEditMode.Should().BeFalse();
        apiMock.Verify(a => a.Portfolio_SaveKpiConfigurationAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_ViewModel_ClearsAndReloadsReport()
    {
        var vm = CreateVm(out var apiMock);
        apiMock.Setup(a => a.Portfolio_ResetCacheAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        apiMock.Setup(a => a.Portfolio_GetAnalysisReportAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReport(777m));

        await vm.RefreshReportAsync();

        apiMock.Verify(a => a.Portfolio_ResetCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
        vm.PortfolioReportData!.Structure.TotalMarketValue.Should().Be(777m);
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private sealed class PassthroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }
}

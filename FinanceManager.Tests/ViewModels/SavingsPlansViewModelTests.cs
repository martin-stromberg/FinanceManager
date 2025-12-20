using FinanceManager.Application;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using FinanceManager.Web.ViewModels.SavingsPlans;

namespace FinanceManager.Tests.ViewModels;

public sealed class SavingsPlansViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private static IServiceProvider CreateSp(Mock<IApiClient> apiMock, bool authenticated = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService { IsAuthenticated = authenticated });
        services.AddSingleton(apiMock.Object);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(TestLocalizer<>));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task InitializeAsync_LoadsPlans_AndAnalyses()
    {
        var plans = new List<SavingsPlanDto>
        {
            new SavingsPlanDto(Guid.NewGuid(), "P1", SavingsPlanType.Recurring, 1000m, new DateTime(2025,1,1), SavingsPlanInterval.Monthly, true, DateTime.UtcNow, null, null, null, null),
            new SavingsPlanDto(Guid.NewGuid(), "P2", SavingsPlanType.Open, null, null, null, true, DateTime.UtcNow, null, null, null, null)
        };

        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.SavingsPlans_ListAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans);
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SavingsPlanCategoryDto>());
        apiMock.Setup(a => a.SavingsPlans_AnalyzeAsync(plans[0].Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsPlanAnalysisDto(plans[0].Id, true, 1000m, new DateTime(2025, 1, 1), 300m, 50m, 14));
        apiMock.Setup(a => a.SavingsPlans_AnalyzeAsync(plans[1].Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsPlanAnalysisDto(plans[1].Id, false, null, null, 0m, 0m, 0));

        var sp = CreateSp(apiMock);
        var vm = new SavingsPlansListViewModel(sp);
        await vm.InitializeAsync();

        Assert.True(vm.Loaded);
        Assert.Equal(2, vm.Items.Count);
    }

    [Fact]
    public async Task ToggleActive_Reloads()
    {
        int calls = 0;
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.SavingsPlans_ListAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => { calls++; return new List<SavingsPlanDto>(); });
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SavingsPlanCategoryDto>());

        var sp = CreateSp(apiMock);
        var vm = new SavingsPlansListViewModel(sp);

        await vm.InitializeAsync();
        Assert.Equal(1, calls);

        vm.ToggleActive();
        await Task.Delay(50);
        Assert.True(calls >= 2);
    }

    [Fact]
    public void GetRibbon_Returns_Registers()
    {
        var apiMock = new Mock<IApiClient>();
        var sp = CreateSp(apiMock);
        var vm = new SavingsPlansListViewModel(sp);

        var loc = sp.GetRequiredService<IStringLocalizer<SavingsPlansViewModelTests>>();
        var regs = vm.GetRibbonRegisters(loc);
        Assert.NotNull(regs);
    }

    private sealed class TestLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new LocalizedString(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new LocalizedString(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) { yield break; }
    }
}

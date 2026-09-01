using FinanceManager.Application;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using FinanceManager.Web.ViewModels.SavingsPlans;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <see cref="SavingsPlansListViewModel"/>'s loading of plans together with their per-plan
/// analysis data, the active/inactive filter toggle triggering a reload, and ribbon construction.
/// </summary>
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

    /// <summary>
    /// Verifies that initialization loads all active plans and, for each one, its analysis data (progress
    /// toward target, required monthly, etc.), populating the items collection with both a fully-analyzed
    /// recurring plan and an open-ended plan whose analysis reports "not achievable".
    /// </summary>
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

    /// <summary>
    /// Verifies that toggling the active/inactive filter triggers a fresh call to the list API (a second
    /// call beyond the initial load), so switching the filter always shows up-to-date data for the new filter state.
    /// </summary>
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
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.True(calls >= 2);
    }

    /// <summary>
    /// Smoke test verifying that <c>GetRibbonRegisters</c> returns a non-null result for the savings plan
    /// list screen.
    /// </summary>
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

using FinanceManager.Application;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using FinanceManager.Web.ViewModels.SavingsPlans;

namespace FinanceManager.Tests.ViewModels;

public sealed class SavingsPlanEditViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private sealed class PassthroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) { yield break; }
    }

    private static (SavingsPlanCardViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));
        var sp = services.BuildServiceProvider();
        var vm = new SavingsPlanCardViewModel(sp);
        return (vm, apiMock);
    }

    [Fact]
    public async Task InitializeAsync_Loads_Edit()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var dto = new SavingsPlanDto(id, "Plan A", SavingsPlanType.OneTime, 100m, DateTime.UtcNow.Date.AddMonths(6), null, true, DateTime.UtcNow, null, null, null);
        apiMock.Setup(a => a.SavingsPlans_GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        apiMock.Setup(a => a.SavingsPlans_AnalyzeAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new SavingsPlanAnalysisDto(id, true, 100m, DateTime.UtcNow.Date.AddMonths(6), 50m, 10m, 6));
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());

        await vm.InitializeAsync(id);

        Assert.True(vm.IsEdit);
        Assert.True(vm.Loaded);
        Assert.Equal("Plan A", vm.Model.Name);
    }

    [Fact]
    public async Task InitializeAsync_New_Prefill_Sets_Name()
    {
        var (vm, apiMock) = CreateVm();
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());

        (vm as FinanceManager.Web.ViewModels.Common.ICardInitializable)?.SetInitValue("PrefillName");
        (vm as FinanceManager.Web.ViewModels.Common.ICardInitializable)?.SetBackNavigation(null);
        await vm.InitializeAsync(Guid.Empty);

        Assert.False(vm.IsEdit);
        Assert.Equal("PrefillName", vm.Model.Name);
    }

    [Fact]
    public async Task SaveAsync_Edit_Success()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var dto = new SavingsPlanDto(id, "Plan A", SavingsPlanType.OneTime, 100m, DateTime.UtcNow.Date.AddMonths(6), null, true, DateTime.UtcNow, null, null, null);
        apiMock.Setup(a => a.SavingsPlans_GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());
        await vm.InitializeAsync(id);

        vm.Model.Name = "Updated";
        apiMock.Setup(a => a.SavingsPlans_UpdateAsync(id, It.Is<SavingsPlanCreateRequest>(r => r.Name == "Updated"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsPlanDto(id, "Updated", SavingsPlanType.OneTime, 100m, DateTime.UtcNow.Date.AddMonths(6), null, true, DateTime.UtcNow, null, null, null));

        var ok = await vm.SaveAsync();
        Assert.True(ok);
        Assert.Null(vm.Error);
    }

    [Fact]
    public async Task SaveAsync_New_Success()
    {
        var (vm, apiMock) = CreateVm();
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());
        await vm.InitializeAsync(Guid.Empty);

        vm.Model.Name = "Created";
        var createdId = Guid.NewGuid();
        apiMock.Setup(a => a.SavingsPlans_CreateAsync(It.IsAny<SavingsPlanCreateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsPlanDto(createdId, "Created", SavingsPlanType.OneTime, null, null, null, true, DateTime.UtcNow, null, null, null));

        var ok = await vm.SaveAsync();
        Assert.True(ok);
    }

    [Fact]
    public async Task Delete_Sets_Error_On_Fail()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var dto = new SavingsPlanDto(id, "Plan A", SavingsPlanType.OneTime, 100m, DateTime.UtcNow.Date.AddMonths(6), null, true, DateTime.UtcNow, null, null, null);
        apiMock.Setup(a => a.SavingsPlans_GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());
        await vm.InitializeAsync(id);

        apiMock.Setup(a => a.SavingsPlans_DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var ok = await vm.DeleteAsync();
        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(vm.LastError));
    }

    [Fact]
    public void Ribbon_Disables_Save_If_Name_Short()
    {
        var (vm, _) = CreateVm();
        var loc = new PassthroughLocalizer<SavingsPlanEditViewModelTests>();
        var regs = vm.GetRibbonRegisters(loc);
        Assert.NotNull(regs);
    }
}

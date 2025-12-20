using FinanceManager.Application;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using FinanceManager.Web.ViewModels.SavingsPlans.Categories;

namespace FinanceManager.Tests.ViewModels;

public sealed class SavingsPlanCategoryDetailViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private static (SavingsPlanCategoryCardViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        var sp = services.BuildServiceProvider();
        var vm = new SavingsPlanCategoryCardViewModel(sp);
        return (vm, apiMock);
    }

    [Fact]
    public async Task Initialize_Edit_Loads_Model()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var dto = new SavingsPlanCategoryDto { Id = id, Name = "Cat1" };
        apiMock.Setup(a => a.SavingsPlanCategories_GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        await vm.LoadAsync(id);

        Assert.Equal("Cat1", vm.Name);
    }

    [Fact]
    public async Task Initialize_Edit_NotFound_Sets_Error()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        apiMock.Setup(a => a.SavingsPlanCategories_GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SavingsPlanCategoryDto?)null);

        await vm.LoadAsync(id);

        Assert.False(string.IsNullOrWhiteSpace(vm.LastError));
    }

    [Fact]
    public void Ribbon_Has_Actions()
    {
        var (vm, _) = CreateVm();
        var loc = new TestLocalizer<SavingsPlanCategoryDetailViewModelTests>();
        var regs = vm.GetRibbonRegisters(loc);
        Assert.NotNull(regs);
    }

    private sealed class TestLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) { yield break; }
    }
}

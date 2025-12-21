using FinanceManager.Application;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Tests.ViewModels;

public sealed class SecurityCategoriesViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager(string baseUri = "http://localhost/") => Initialize(baseUri, baseUri);
        protected override void NavigateToCore(string uri, bool forceLoad) { /* no-op for tests */ }
    }

    private static (FinanceManager.Web.ViewModels.Securities.Categories.SecurityCategoriesListViewModel vm, Mock<IApiClient> apiMock) CreateVm(bool authenticated = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService { IsAuthenticated = authenticated });
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        // Register required framework services used by viewmodels
        services.AddSingleton<NavigationManager>(new TestNavigationManager());
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(TestLocalizer<>));
        var sp = services.BuildServiceProvider();
        var vm = new FinanceManager.Web.ViewModels.Securities.Categories.SecurityCategoriesListViewModel(sp);
        return (vm, apiMock);
    }

    [Fact]
    public async Task Initialize_Loads_Categories()
    {
        var (vm, apiMock) = CreateVm();
        var categories = new List<SecurityCategoryDto>
        {
            new SecurityCategoryDto { Id = Guid.NewGuid(), Name = "A" },
            new SecurityCategoryDto { Id = Guid.NewGuid(), Name = "B" }
        };
        apiMock.Setup(a => a.SecurityCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);

        await vm.InitializeAsync();

        Assert.True(vm.Loaded);
        Assert.Equal(2, vm.Items.Count);
        Assert.Equal(new[] { "A", "B" }, vm.Items.Select(x => x.Name).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task Initialize_RequiresAuth_When_NotAuthenticated()
    {
        var (vm, apiMock) = CreateVm(authenticated: false);
        bool authRequired = false;
        vm.AuthenticationRequired += (_, __) => authRequired = true;

        await vm.InitializeAsync();

        Assert.False(vm.Loaded);
        Assert.True(authRequired);
        apiMock.Verify(a => a.SecurityCategories_ListAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Ribbon_Has_Actions()
    {
        var (vm, _) = CreateVm();
        var loc = new TestLocalizer<SecurityCategoriesViewModelTests>();
        var regs = vm.GetRibbon(loc);

        // collect actions from explicit tabs (modern shape)
        var actionsFromTabs = regs.SelectMany(r => r.Tabs ?? new List<UiRibbonTab>()).SelectMany(t => t.Items ?? new List<UiRibbonAction>()).ToList();
        // collect actions from legacy flat Items property on registers (older viewmodels/tests rely on this)
        var actionsFromRegisters = regs.SelectMany(r => r.Items ?? new List<UiRibbonAction>()).ToList();

        // combine both sources to be resilient to grouping changes in viewmodels
        var allActions = actionsFromTabs.Concat(actionsFromRegisters).Distinct().ToList();

        Assert.Contains(allActions, i => i.Action == "New");
        Assert.Contains(allActions, i => i.Action == "Back");
    }

    private sealed class TestLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) { yield break; }
    }
}

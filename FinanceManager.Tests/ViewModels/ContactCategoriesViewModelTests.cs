using FinanceManager.Application;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Tests.ViewModels;

public sealed class ContactCategoriesViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId => Guid.NewGuid();
        public string? PreferredLanguage => "de";
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin => false;
    }

    private sealed class DummyLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }

    // Simple test NavigationManager to satisfy DI for viewmodels that require it
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("http://localhost/", "http://localhost/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            // no-op for tests
        }
    }

    private static (ContactCategoriesViewModel vm, Mock<IApiClient> apiMock) CreateVm(bool isAuthenticated = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService { IsAuthenticated = isAuthenticated });
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        // register a test NavigationManager so ViewModels can request it in tests
        services.AddSingleton<NavigationManager>(new TestNavigationManager());
        var sp = services.BuildServiceProvider();
        var vm = new ContactCategoriesViewModel(sp);
        return (vm, apiMock);
    }

    [Fact]
    public async Task Initialize_LoadsCategories_WhenAuthenticated()
    {
        var (vm, apiMock) = CreateVm();
        var categories = new List<ContactCategoryDto>
        {
            new ContactCategoryDto(Guid.NewGuid(), "A", null),
            new ContactCategoryDto(Guid.NewGuid(), "B", null)
        };
        apiMock.Setup(a => a.ContactCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);

        await vm.InitializeAsync();

        Assert.True(vm.Loaded);
        Assert.Equal(2, vm.Categories.Count);
        Assert.Contains(vm.Categories, c => c.Name == "A");
    }

    [Fact]
    public async Task Initialize_RequiresAuth_WhenNotAuthenticated()
    {
        var (vm, apiMock) = CreateVm(isAuthenticated: false);
        var authEvents = 0;
        vm.AuthenticationRequired += (_, __) => authEvents++;

        await vm.InitializeAsync();

        Assert.Equal(1, authEvents);
        Assert.False(vm.Loaded);
        apiMock.Verify(a => a.ContactCategories_ListAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Posts_SetsBusy_ResetsName_AndReloads()
    {
        var (vm, apiMock) = CreateVm();
        var createdId = Guid.NewGuid();
        var createdDto = new ContactCategoryDto(createdId, "X", null);

        apiMock.Setup(a => a.ContactCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContactCategoryDto>());
        apiMock.Setup(a => a.ContactCategories_CreateAsync(It.IsAny<ContactCategoryCreateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdDto);

        await vm.InitializeAsync();
        vm.CreateName = "New";

        var task = vm.CreateAsync();
        Assert.True(vm.Busy);
        await task;

        apiMock.Verify(a => a.ContactCategories_CreateAsync(It.Is<ContactCategoryCreateRequest>(r => r.Name == "New"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(vm.Busy);
        Assert.Equal(string.Empty, vm.CreateName);
    }

    [Fact]
    public async Task CreateAsync_SetsError_OnFailure()
    {
        var (vm, apiMock) = CreateVm();
        apiMock.Setup(a => a.ContactCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContactCategoryDto>());
        apiMock.Setup(a => a.ContactCategories_CreateAsync(It.IsAny<ContactCategoryCreateRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("bad"));

        await vm.InitializeAsync();
        vm.CreateName = "New";

        await vm.CreateAsync();

        Assert.False(string.IsNullOrWhiteSpace(vm.Error));
        Assert.False(vm.Busy);
    }

    [Fact]
    public void GetRibbon_ContainsExpectedGroups()
    {
        var (vm, _) = CreateVm();
        var loc = new DummyLocalizer();

        var groups = vm.GetRibbon(loc);
        Assert.Contains(groups, g => g.Tabs != null && g.Tabs.Any(t => t.Title == "Ribbon_Group_Navigation"));
        Assert.Contains(groups, g => g.Tabs != null && g.Tabs.Any(t => t.Title == "Ribbon_Group_Actions"));
        Assert.Contains(groups.SelectMany(r => r.Tabs.SelectMany(t => t.Items)), i => i.Action == "Back");
        Assert.Contains(groups.SelectMany(r => r.Tabs.SelectMany(t => t.Items)), i => i.Action == "New");
    }
}

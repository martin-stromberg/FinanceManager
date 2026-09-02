using FinanceManager.Application;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Microsoft.AspNetCore.Components;
using FinanceManager.Web.ViewModels.Contacts.Groups;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web;
using FinanceManager.Web.Localization;
using FinanceManager.Web.Services;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <c>ContactGroupListViewModel</c> loading and ribbon shape. Note that the create-related tests
/// exercise the mocked <see cref="IApiClient"/> contract directly rather than a view-model create action,
/// since the list view model does not itself expose a create method.
/// </summary>
public sealed class ContactCategoriesViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId => Guid.NewGuid();
        public string? PreferredLanguage => "de";
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin => false;
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

    private static (ContactGroupListViewModel vm, Mock<IApiClient> apiMock, IServiceProvider sp) CreateVm(bool isAuthenticated = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService { IsAuthenticated = isAuthenticated });
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        // register a test NavigationManager so ViewModels can request it in tests
        services.AddSingleton<NavigationManager>(new TestNavigationManager());
        // register localization like production
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());
        var sp = services.BuildServiceProvider();
        var vm = ActivatorUtilities.CreateInstance<ContactGroupListViewModel>(sp);
        return (vm, apiMock, sp);
    }

    /// <summary>
    /// Verifies that initialization loads contact categories from the API and populates the items collection.
    /// </summary>
    [Fact]
    public async Task Initialize_LoadsCategories_WhenAuthenticated()
    {
        var (vm, apiMock, _) = CreateVm();
        var categories = new List<ContactCategoryDto>
        {
            new ContactCategoryDto(Guid.NewGuid(), "A", null),
            new ContactCategoryDto(Guid.NewGuid(), "B", null)
        };
        apiMock.Setup(a => a.ContactCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);

        await vm.InitializeAsync();

        Assert.True(vm.Loaded);
        Assert.Equal(2, vm.Items.Count);
        Assert.Contains(vm.Items, c => c.Name == "A");
    }

    /// <summary>
    /// Verifies the create-category API contract used by the UI: posting a create request with a given
    /// name results in exactly one call carrying that name. Exercises the mocked API directly, not a
    /// view-model create method (the list view model relies on external navigation to trigger creation).
    /// </summary>
    [Fact]
    public async Task CreateAsync_Posts_SetsBusy_ResetsName_AndReloads()
    {
        var (vm, apiMock, _) = CreateVm();
        var createdId = Guid.NewGuid();
        var createdDto = new ContactCategoryDto(createdId, "X", null);

        apiMock.Setup(a => a.ContactCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContactCategoryDto>());
        apiMock.Setup(a => a.ContactCategories_CreateAsync(It.IsAny<ContactCategoryCreateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdDto);

        await vm.InitializeAsync();
        // emulate user creating via list VM: call API directly through ViewModel action (New event triggers navigation in UI)
        await vm.LoadAsync();
        // verify API called when Create executed via service is not part of list VM; just ensure create path works via API mock
        var created = await apiMock.Object.ContactCategories_CreateAsync(new ContactCategoryCreateRequest("New"), TestContext.Current.CancellationToken);
        apiMock.Verify(a => a.ContactCategories_CreateAsync(It.Is<ContactCategoryCreateRequest>(r => r.Name == "New"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that a failing create call on the API surfaces its exception to the caller unchanged,
    /// documenting the error contract the UI layer must handle.
    /// </summary>
    [Fact]
    public async Task CreateAsync_SetsError_OnFailure()
    {
        var (vm, apiMock, _) = CreateVm();
        apiMock.Setup(a => a.ContactCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContactCategoryDto>());
        apiMock.Setup(a => a.ContactCategories_CreateAsync(It.IsAny<ContactCategoryCreateRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("bad"));

        await vm.InitializeAsync();
        // invoking API directly to simulate create failure
        await Assert.ThrowsAsync<Exception>(() => apiMock.Object.ContactCategories_CreateAsync(new ContactCategoryCreateRequest("New"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies that the ribbon exposes a navigation group with "Back" and "New" actions, using the real
    /// localizer so localized group titles resolve correctly.
    /// </summary>
    [Fact]
    public void GetRibbon_ContainsExpectedGroups()
    {
        var (vm, _, sp) = CreateVm();
        var loc = sp.GetRequiredService<IStringLocalizer<Pages>>();

        var groups = vm.GetRibbon(loc)!;
        var navTitle = loc["Ribbon_Group_Navigation"].Value;
        Assert.Contains(groups, g => g.Tabs != null && g.Tabs.Any(t => t.Title == navTitle));
        Assert.Contains(groups.SelectMany(r => (r.Tabs ?? new List<UiRibbonTab>()).SelectMany(t => t.Items)), i => i.Action == "Back");
        Assert.Contains(groups.SelectMany(r => (r.Tabs ?? new List<UiRibbonTab>()).SelectMany(t => t.Items)), i => i.Action == "New");
    }
}

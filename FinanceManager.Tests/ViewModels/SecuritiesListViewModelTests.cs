using FinanceManager.Application;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <see cref="SecuritiesListViewModel"/>'s loading behavior, the active/inactive filter toggle
/// triggering a reload, and authentication gating.
/// </summary>
public sealed class SecuritiesListViewModelTests
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
        public TestNavigationManager(string baseUri = "http://localhost/")
        {
            Initialize(baseUri, baseUri);
        }
        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            // no-op for tests
        }
    }

    private sealed class PassthroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) { yield break; }
    }

    private static (SecuritiesListViewModel vm, Mock<IApiClient> apiMock) CreateVm(bool authenticated = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService { IsAuthenticated = authenticated });
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        // register NavigationManager and localizer required by viewmodels
        services.AddSingleton<NavigationManager>(new TestNavigationManager());
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));
        var sp = services.BuildServiceProvider();
        var vm = new SecuritiesListViewModel(sp);
        return (vm, apiMock);
    }

    /// <summary>
    /// Verifies that initialization loads active securities and their categories into the items collection.
    /// </summary>
    [Fact]
    public async Task Initialize_Loads_List()
    {
        var (vm, apiMock) = CreateVm();
        var items = new List<SecurityDto>
        {
            new SecurityDto { Id = Guid.NewGuid(), Name = "A", Identifier = "A1" },
            new SecurityDto { Id = Guid.NewGuid(), Name = "B", Identifier = "B1" }
        };
        apiMock.Setup(a => a.Securities_ListAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        apiMock.Setup(a => a.SecurityCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecurityCategoryDto>());

        await vm.InitializeAsync();

        Assert.True(vm.Loaded);
        Assert.Equal(2, vm.Items.Count);
    }

    /// <summary>
    /// Verifies that toggling the active/inactive filter triggers exactly one additional call to the list
    /// API beyond the initial load, so switching filters always shows current data for the new state.
    /// </summary>
    [Fact]
    public async Task ToggleActive_Reloads()
    {
        var (vm, apiMock) = CreateVm();
        int calls = 0;
        apiMock.Setup(a => a.Securities_ListAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                calls++;
                return new List<SecurityDto>();
            });
        apiMock.Setup(a => a.SecurityCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecurityCategoryDto>());

        await vm.InitializeAsync();
        Assert.Equal(1, calls);

        vm.ToggleActive();
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(2, calls);
    }

    /// <summary>
    /// The securities overview hides the date range filter — securities are master data without a
    /// list-relevant date.
    /// </summary>
    [Fact]
    public void AllowRangeFiltering_IsFalse()
    {
        var (vm, _) = CreateVm();
        Assert.False(vm.AllowRangeFiltering);
    }

    /// <summary>
    /// A search term filters the list across name, identifier, AlphaVantage code and category name
    /// (substring, case-insensitive); clearing it restores the full list.
    /// </summary>
    [Fact]
    public async Task Search_FiltersAcrossFields()
    {
        var (vm, apiMock) = CreateVm();
        var items = new List<SecurityDto>
        {
            new SecurityDto { Id = Guid.NewGuid(), Name = "Alpha ETF", Identifier = "A1" },
            new SecurityDto { Id = Guid.NewGuid(), Name = "Beta AG", Identifier = "B2", AlphaVantageCode = "BETA" },
            new SecurityDto { Id = Guid.NewGuid(), Name = "Gamma", Identifier = "G3", CategoryName = "Rohstoffe" }
        };
        apiMock.Setup(a => a.Securities_ListAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        apiMock.Setup(a => a.SecurityCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecurityCategoryDto>());

        vm.SetSearch("alpha etf");
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Single(vm.Items);
        Assert.Equal("Alpha ETF", vm.Items[0].Name);

        vm.SetSearch("beta");
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Single(vm.Items);
        Assert.Equal("Beta AG", vm.Items[0].Name);

        vm.SetSearch("rohstoffe");
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Single(vm.Items);
        Assert.Equal("Gamma", vm.Items[0].Name);

        vm.SetSearch(string.Empty);
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, vm.Items.Count);
    }

    /// <summary>
    /// Verifies that an unauthenticated user never reaches the securities API: initialization raises
    /// <c>AuthenticationRequired</c> and leaves the view model unloaded instead of calling the list endpoint.
    /// </summary>
    [Fact]
    public async Task Initialize_RequiresAuth_When_NotAuthenticated()
    {
        var (vm, apiMock) = CreateVm(authenticated: false);
        bool authRequired = false;
        vm.AuthenticationRequired += (_, __) => authRequired = true;

        await vm.InitializeAsync();

        Assert.False(vm.Loaded);
        Assert.True(authRequired);
        apiMock.Verify(a => a.Securities_ListAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

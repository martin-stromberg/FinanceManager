using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.Services;
using FinanceManager.Web.ViewModels.Securities.Prices;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <see cref="SecurityPricesListViewModel"/>'s paging behavior, the "backfill"/"import" overlay
/// requests it raises via <c>UiActionRequested</c>, and the ribbon's "ImportPrices" action - including that
/// it is disabled when no concrete security id is bound (an empty-id list view).
/// </summary>
public sealed class SecurityPricesViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; } = false;
    }

    private static (SecurityPricesListViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());
        services.AddSingleton<NavigationManager>(new TestNavigationManager());
        var sp = services.BuildServiceProvider();
        var vm = new SecurityPricesListViewModel(sp, Guid.NewGuid());
        return (vm, apiMock);
    }

    private static List<SecurityPriceDto> CreatePrices(int count, DateTime? start = null)
    {
        var s = start ?? new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return Enumerable.Range(0, count)
            .Select(i => new SecurityPriceDto(s.AddDays(i), 100 + i))
            .ToList();
    }

    /// <summary>
    /// Verifies that initialization loads the first page of prices, correctly infers there is no further
    /// page (fewer items than the page size), and raises at least two <c>StateChanged</c> notifications
    /// (loading start and completion) so bound UI components refresh.
    /// </summary>
    [Fact]
    public async Task Initialize_LoadsFirstPage_SetsItemsAndFlags()
    {
        var (vm, apiMock) = CreateVm();
        apiMock.Setup(a => a.Securities_GetPricesAsync(It.IsAny<Guid>(), 0, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePrices(2));

        var events = 0;
        vm.StateChanged += (_, __) => events++;

        await vm.InitializeAsync();

        Assert.False(vm.Loading);
        Assert.Equal(2, vm.Items.Count);
        Assert.False(vm.CanLoadMore);
        Assert.True(events >= 2);
    }

    /// <summary>
    /// Verifies that loading more pages of prices appends to the existing collection and that "can load
    /// more" flips off once a page comes back shorter than the fixed page size (100).
    /// </summary>
    [Fact]
    public async Task LoadMore_AppendsItems_StopsWhenBelowPageSize()
    {
        var (vm, apiMock) = CreateVm();
        int call = 0;
        apiMock.Setup(a => a.Securities_GetPricesAsync(It.IsAny<Guid>(), It.IsAny<int>(), 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                call++;
                return CreatePrices(call == 1 ? 100 : 50);
            });

        await vm.InitializeAsync();
        Assert.Equal(100, vm.Items.Count);
        Assert.True(vm.CanLoadMore);

        await vm.LoadMoreAsync();
        Assert.Equal(150, vm.Items.Count);
        Assert.False(vm.CanLoadMore);
        Assert.False(vm.Loading);
    }

    /// <summary>
    /// Verifies that requesting the price backfill dialog raises an "OpenOverlay" UI action with a
    /// payload object describing the overlay to show, rather than the view model opening it directly -
    /// keeping overlay presentation as the host component's responsibility.
    /// </summary>
    [Fact]
    public void RequestOpenBackfill_RaisesUiAction()
    {
        var (vm, _) = CreateVm();
        BaseViewModel.UiActionEventArgs? received = null;
        vm.UiActionRequested += (_, e) => received = e;

        vm.RequestOpenBackfill();

        Assert.NotNull(received);
        Assert.Equal("OpenOverlay", received!.Action);
        Assert.Null(received.Payload);
        Assert.NotNull(received.PayloadObject);
    }

    /// <summary>
    /// Verifies that invoking the ribbon's "ImportPrices" action raises a UI action whose payload
    /// specifies the <see cref="SecurityPriceImportPanel"/> component as the overlay to open.
    /// </summary>
    [Fact]
    public async Task RibbonImportPricesAction_ShouldOpenImportOverlay()
    {
        var (vm, _) = CreateVm();
        await vm.InitializeAsync();

        var action = vm.GetRibbon(new TestLocalizer<Pages>())!
            .SelectMany(x => x.Tabs ?? new List<UiRibbonTab>())
            .SelectMany(x => x.Items ?? new List<UiRibbonAction>())
            .Single(x => x.Action == "ImportPrices");

        BaseViewModel.UiActionEventArgs? received = null;
        vm.UiActionRequested += (_, e) => received = e;

        await action.Callback!();

        Assert.NotNull(received);
        var overlay = Assert.IsType<BaseViewModel.UiOverlaySpec>(received!.PayloadObject);
        Assert.Equal(typeof(SecurityPriceImportPanel), overlay.ComponentType);
    }

    /// <summary>
    /// Verifies that <c>RequestOpenImport</c> raises an overlay spec targeting the import panel with the
    /// bound security id and an overlay title supplied as parameters, so the import dialog opens
    /// pre-scoped to the correct security.
    /// </summary>
    [Fact]
    public void RequestOpenImport_RaisesOverlayWithImportPanelAndSecurityId()
    {
        var (vm, _) = CreateVm();
        BaseViewModel.UiActionEventArgs? received = null;
        vm.UiActionRequested += (_, e) => received = e;

        vm.RequestOpenImport();

        Assert.NotNull(received);
        Assert.Equal("OpenOverlay", received!.Action);
        var overlay = Assert.IsType<BaseViewModel.UiOverlaySpec>(received.PayloadObject);
        Assert.Equal(typeof(SecurityPriceImportPanel), overlay.ComponentType);
        Assert.NotNull(overlay.Parameters);
        Assert.Equal(vm.SecurityId, Assert.IsType<Guid>(overlay.Parameters!["SecurityId"]));
        Assert.True(overlay.Parameters.ContainsKey("OverlayTitle"));
    }

    /// <summary>
    /// Verifies that the ribbon's "ImportPrices" action is disabled when the view model is constructed
    /// with an empty security id, preventing the user from importing prices into a not-yet-selected security.
    /// </summary>
    [Fact]
    public async Task RibbonImportPricesAction_ShouldBeDisabled_WhenSecurityIdIsEmpty()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton(new Mock<IApiClient>().Object);
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());
        services.AddSingleton<NavigationManager>(new TestNavigationManager());
        var vm = new SecurityPricesListViewModel(services.BuildServiceProvider(), Guid.Empty);

        await vm.InitializeAsync();

        var action = vm.GetRibbon(new TestLocalizer<Pages>())!
            .SelectMany(x => x.Tabs ?? new List<UiRibbonTab>())
            .SelectMany(x => x.Items ?? new List<UiRibbonAction>())
            .Single(x => x.Action == "ImportPrices");

        Assert.True(action.Disabled);
    }

    private sealed class TestLocalizer<T> : Microsoft.Extensions.Localization.IStringLocalizer<T>
    {
        public Microsoft.Extensions.Localization.LocalizedString this[string name] => new(name, name);
        public Microsoft.Extensions.Localization.LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<Microsoft.Extensions.Localization.LocalizedString> GetAllStrings(bool includeParentCultures) { yield break; }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            // initialize with a base and current URI
            Initialize("http://localhost/", "http://localhost/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            // no-op for tests
        }
    }
}

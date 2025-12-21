using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Web;
using FinanceManager.Web.Services;
using FinanceManager.Web.ViewModels.Securities.Prices;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.ViewModels;

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

    [Fact]
    public void RequestOpenBackfill_RaisesUiAction()
    {
        var (vm, _) = CreateVm();
        FinanceManager.Web.ViewModels.Common.BaseViewModel.UiActionEventArgs? received = null;
        vm.UiActionRequested += (_, e) => received = e;

        vm.RequestOpenBackfill();

        Assert.NotNull(received);
        Assert.Equal("OpenOverlay", received!.Action);
        Assert.Null(received.Payload);
        Assert.NotNull(received.PayloadObject);
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

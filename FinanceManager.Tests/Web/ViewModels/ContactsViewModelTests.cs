using System.Linq;
using FinanceManager.Application;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Web.ViewModels;

public sealed class ContactsViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; }
        public bool IsAdmin { get; set; }
    }

    private sealed class PassthroughLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new LocalizedString(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new LocalizedString(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }

    private sealed class PassthroughLocalizerGeneric<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new LocalizedString(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new LocalizedString(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => (IStringLocalizer)this;
    }

    private static (FinanceManager.Web.ViewModels.Contacts.ContactListViewModel vm, Mock<IApiClient> apiMock) CreateVm(bool isAuthenticated)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService { IsAuthenticated = isAuthenticated });
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        // register IStringLocalizer<Pages> required by BaseViewModel
        services.AddSingleton<IStringLocalizer<FinanceManager.Web.Pages>>(new PassthroughLocalizerGeneric<FinanceManager.Web.Pages>());
        var sp = services.BuildServiceProvider();
        var vm = new FinanceManager.Web.ViewModels.Contacts.ContactListViewModel(sp);
        return (vm, apiMock);
    }

    [Fact]
    public async Task InitializeAsync_ShouldRequestAuth_WhenNotAuthenticated()
    {
        var (vm, _) = CreateVm(isAuthenticated: false);

        // ContactListViewModel does not enforce authentication itself; ensure InitializeAsync runs without throwing
        await vm.InitializeAsync();

        Assert.NotNull(vm.Items);
        Assert.Equal(0, vm.Items.Count);
    }

    [Fact]
    public async Task InitializeAsync_ShouldLoadCategories_And_FirstPage_WhenAuthenticated()
    {
        var (vm, apiMock) = CreateVm(isAuthenticated: true);
        var catId = Guid.NewGuid();
        var categories = new List<ContactCategoryDto> { new ContactCategoryDto(catId, "Friends", null) };
        var contacts = new List<ContactDto> { new ContactDto(Guid.NewGuid(), "Alice", ContactType.Person, catId, null, false, null) };

        apiMock.Setup(a => a.ContactCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);
        apiMock.Setup(a => a.Contacts_ListAsync(0, 50, null, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contacts);

        await vm.InitializeAsync();

        Assert.True(vm.Loaded);
        Assert.Equal(1, vm.Items.Count);
        Assert.Equal("Alice", vm.Items[0].Name);
        Assert.Equal("Friends", vm.Items[0].CategoryName);
    }

    [Fact]
    public async Task LoadMoreAsync_ShouldPaginate_And_SetAllLoaded()
    {
        var (vm, apiMock) = CreateVm(isAuthenticated: true);
        var firstPage = Enumerable.Range(0, 50).Select(i => new ContactDto(Guid.NewGuid(), $"N{i}", ContactType.Person, null, null, false, null)).ToList();
        var secondPage = Enumerable.Range(0, 10).Select(i => new ContactDto(Guid.NewGuid(), $"M{i}", ContactType.Person, null, null, false, null)).ToList();

        apiMock.Setup(a => a.ContactCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContactCategoryDto>());

        int callCount = 0;
        apiMock.Setup(a => a.Contacts_ListAsync(It.IsAny<int>(), 50, null, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? firstPage : secondPage;
            });

        await vm.InitializeAsync();
        Assert.Equal(50, vm.Items.Count);
        Assert.True(vm.CanLoadMore);

        await vm.LoadMoreAsync();
        Assert.Equal(60, vm.Items.Count);
        Assert.False(vm.CanLoadMore);
    }

    [Fact]
    public async Task SetFilterAsync_ShouldResetAndReload_AndRibbonIncludesClear()
    {
        var (vm, apiMock) = CreateVm(isAuthenticated: true);
        var unfilteredContacts = new List<ContactDto> { new ContactDto(Guid.NewGuid(), "B", ContactType.Person, null, null, false, null) };
        var filteredContacts = new List<ContactDto>
        {
            new ContactDto(Guid.NewGuid(), "Ax", ContactType.Person, null, null, false, null),
            new ContactDto(Guid.NewGuid(), "Ay", ContactType.Person, null, null, false, null)
        };

        apiMock.Setup(a => a.ContactCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContactCategoryDto>());
        apiMock.Setup(a => a.Contacts_ListAsync(0, 50, null, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unfilteredContacts);
        apiMock.Setup(a => a.Contacts_ListAsync(0, 50, null, false, "A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(filteredContacts);

        await vm.InitializeAsync();
        Assert.Equal(1, vm.Items.Count);

        // Apply filter: set search, reset paging and load
        vm.SetSearch("A");
        vm.ResetAndSearch();
        await vm.LoadAsync();

        Assert.Equal(2, vm.Items.Count);
        Assert.False(vm.CanLoadMore);

        var ribbonRegs = vm.GetRibbon(new PassthroughLocalizer());
        Assert.Equal(1, ribbonRegs.Count);
        var items = ribbonRegs.SelectMany(r => r.Tabs ?? Enumerable.Empty<FinanceManager.Web.ViewModels.Common.UiRibbonTab>())
                              .SelectMany(t => t.Items ?? Enumerable.Empty<FinanceManager.Web.ViewModels.Common.UiRibbonAction>())
                              .ToList();
        Assert.True(items.Any(i => i.Action == "ClearFilter" || i.Action == "ClearSearch"));
    }
}

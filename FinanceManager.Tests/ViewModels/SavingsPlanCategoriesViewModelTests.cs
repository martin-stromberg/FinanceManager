using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Web;
using FinanceManager.Web.Services;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.SavingsPlans.Categories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <see cref="SavingsPlanCategoryListViewModel"/>'s loading behavior, authentication gating, and
/// ribbon action set (Back/New) for the savings plan category list screen.
/// </summary>
public sealed class SavingsPlanCategoriesViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private static (SavingsPlanCategoryListViewModel vm, Mock<IApiClient> apiMock, ServiceProvider sp) CreateVm(bool authenticated = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService { IsAuthenticated = authenticated });
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());
        var sp = services.BuildServiceProvider();
        var vm = new SavingsPlanCategoryListViewModel(sp);
        return (vm, apiMock, sp);
    }

    /// <summary>
    /// Verifies that initialization loads all savings plan categories from the API into the items collection.
    /// </summary>
    [Fact]
    public async Task Initialize_Loads_Categories()
    {
        var (vm, apiMock, _) = CreateVm();
        var categories = new List<SavingsPlanCategoryDto>
        {
            new SavingsPlanCategoryDto { Id = Guid.NewGuid(), Name = "A" },
            new SavingsPlanCategoryDto { Id = Guid.NewGuid(), Name = "B" }
        };
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);

        await vm.InitializeAsync();

        Assert.True(vm.Loaded);
        Assert.Equal(2, vm.Items.Count);
        Assert.Equal(new[] { "A", "B" }, vm.Items.Select(x => x.Name).OrderBy(x => x).ToArray());
    }

    /// <summary>
    /// Verifies that an unauthenticated user never reaches the categories API: initialization raises
    /// <c>AuthenticationRequired</c> and leaves the view model unloaded instead of calling the list endpoint.
    /// </summary>
    [Fact]
    public async Task Initialize_RequiresAuth_When_NotAuthenticated()
    {
        var (vm, apiMock, _) = CreateVm(authenticated: false);
        bool authRequired = false;
        vm.AuthenticationRequired += (_, __) => authRequired = true;

        await vm.InitializeAsync();

        Assert.False(vm.Loaded);
        Assert.True(authRequired);
        apiMock.Verify(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that the ribbon exposes both "Back" and "New" actions somewhere across its groups,
    /// checked in a flattened way so the test does not depend on a specific grouping structure.
    /// </summary>
    [Fact]
    public void Ribbon_Has_Actions()
    {
        var (vm, _, sp) = CreateVm();
        var loc = sp.GetRequiredService<IStringLocalizer<Pages>>();
        var regs = vm.GetRibbonRegisters(loc);
        var groups = regs.ToUiRibbonGroups(loc);
        var actions = groups.First();
        Assert.Contains(actions.Items, i => i.Action == "Back");
        // Flatten groups to avoid relying on specific grouping order/structure in viewmodels
        var items = groups.SelectMany(g => g.Items).ToList();
        Assert.Contains(items, i => i.Action == "Back");
        Assert.Contains(items, i => i.Action == "New");
    }
}

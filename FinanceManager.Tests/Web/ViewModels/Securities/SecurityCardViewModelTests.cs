using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.Securities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Web.ViewModels.Securities;

public sealed class SecurityCardViewModelTests
{
    /// <summary>
    /// Verifies that the security card no longer exposes the import action in the linked ribbon tab.
    /// </summary>
    [Fact]
    public async Task GetRibbonRegisters_ShouldNotExposeImportPricesAction()
    {
        var vm = CreateVm(out _);
        await vm.LoadAsync(Guid.Empty);

        var actions = GetActions(vm);
        Assert.DoesNotContain(actions, x => x.Action == "ImportPrices");
    }

    /// <summary>
    /// Verifies that prices navigation remains available and enabled for persisted securities.
    /// </summary>
    [Fact]
    public async Task PricesAction_ShouldRaiseOpenPricesNavigation_WhenIdIsValid()
    {
        var vm = CreateVm(out var apiMock);
        var securityId = Guid.NewGuid();
        apiMock.Setup(x => x.Securities_GetAsync(securityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityDto
            {
                Id = securityId,
                Name = "ETF",
                Identifier = "ISIN123",
                CurrencyCode = "EUR",
                IsActive = true
            });
        apiMock.Setup(x => x.SecurityCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SecurityCategoryDto>());

        BaseViewModel.UiActionEventArgs? raisedAction = null;
        vm.UiActionRequested += (_, e) => raisedAction = e;

        await vm.LoadAsync(securityId);
        var action = GetAction(vm, "Prices");
        await action.Callback!();

        Assert.NotNull(raisedAction);
        Assert.Equal("OpenPrices", raisedAction!.Action);
        Assert.Equal($"/list/securities/prices/{securityId}", raisedAction.Payload);
    }

    private static SecurityCardViewModel CreateVm(out Mock<IApiClient> apiMock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        services.AddSingleton<IStringLocalizer<FinanceManager.Web.Pages>>(new PassthroughLocalizer<FinanceManager.Web.Pages>());
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));
        return new SecurityCardViewModel(services.BuildServiceProvider());
    }

    private static IReadOnlyList<UiRibbonAction> GetActions(SecurityCardViewModel vm)
        => vm.GetRibbon(new PassthroughLocalizer())!
            .SelectMany(x => x.Tabs ?? new List<UiRibbonTab>())
            .SelectMany(x => x.Items ?? new List<UiRibbonAction>())
            .ToList();

    private static UiRibbonAction GetAction(SecurityCardViewModel vm, string actionId)
        => GetActions(vm).Single(x => x.Action == actionId);

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private sealed class PassthroughLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }

    private sealed class PassthroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }
}

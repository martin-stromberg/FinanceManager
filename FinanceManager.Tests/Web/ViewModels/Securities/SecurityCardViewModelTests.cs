using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.Securities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Web.ViewModels.Securities;

/// <summary>
/// Tests for the security detail card's <c>SecurityCardViewModel</c> ribbon wiring, covering the removal of
/// the legacy import-prices action and the navigation action that opens the security's price history.
/// </summary>
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
        Assert.Equal($"list/securities/prices/{securityId}", raisedAction.Payload);
    }

    /// <summary>
    /// The "Zurücksetzen" action must sit in the manage group directly behind Save as a small
    /// item with the undo icon, no mobile shortcut and a disabled state while no pending
    /// changes exist.
    /// </summary>
    [Fact]
    public async Task Ribbon_ContainsReset_AfterSave_InManageGroup()
    {
        var vm = CreateVm(out var apiMock);
        var securityId = Guid.NewGuid();
        SetupSecurity(apiMock, securityId);
        await vm.LoadAsync(securityId);

        var manage = GetTab(vm, "Ribbon_Group_Manage");
        var ids = manage.Items.Select(i => i.Id).ToList();
        var saveIndex = ids.IndexOf("Save");
        Assert.True(saveIndex >= 0);
        Assert.Equal(saveIndex + 1, ids.IndexOf("Reset"));

        var reset = manage.Items[saveIndex + 1];
        Assert.Equal(UiRibbonItemSize.Small, reset.Size);
        Assert.Contains("#undo", reset.IconSvg);
        Assert.Equal("Ribbon_Reset", reset.Label);
        Assert.False(reset.MobileShortcut);
        Assert.True(reset.Disabled);
    }

    /// <summary>
    /// Invoking the Reset action must discard pending edits and rebuild the card record from
    /// the loaded security dto - without fetching the security from the API again.
    /// </summary>
    [Fact]
    public async Task Reset_DiscardsPendingChanges_AndRestoresBaseline()
    {
        var vm = CreateVm(out var apiMock);
        var securityId = Guid.NewGuid();
        SetupSecurity(apiMock, securityId);
        await vm.LoadAsync(securityId);

        var nameField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_Security_Name");
        vm.ValidateFieldValue(nameField, "Changed");
        Assert.True(vm.HasPendingChanges);

        var reset = GetAction(vm, "Reset");
        Assert.False(reset.Disabled);
        await reset.Callback!();

        Assert.False(vm.HasPendingChanges);
        Assert.Equal("ETF", vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_Security_Name").Text);
        apiMock.Verify(x => x.Securities_GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// When the update call fails the save path must roll the mutated edit model back to the
    /// pre-save state, so a subsequent Reset rebuilds the card from the persisted dto baseline.
    /// </summary>
    [Fact]
    public async Task Reset_AfterFailedSave_RestoresPersistedBaseline()
    {
        var vm = CreateVm(out var apiMock);
        var securityId = Guid.NewGuid();
        SetupSecurity(apiMock, securityId);
        apiMock.Setup(x => x.Securities_UpdateAsync(securityId, It.IsAny<SecurityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SecurityDto?)null);
        await vm.LoadAsync(securityId);

        var nameField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_Security_Name");
        vm.ValidateFieldValue(nameField, "Changed");

        var saved = await vm.SaveAsync(TestContext.Current.CancellationToken);
        Assert.Null(saved);
        Assert.Equal(string.Empty, vm.Model.Name);

        await GetAction(vm, "Reset").Callback!();

        Assert.False(vm.HasPendingChanges);
        Assert.Equal("ETF", vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_Security_Name").Text);
    }

    /// <summary>
    /// After a successful save the pending set must be cleared so Save and Reset are disabled
    /// again - consistent with the other card view models.
    /// </summary>
    [Fact]
    public async Task Save_Success_ClearsPendingChanges_AndDisablesSaveAndReset()
    {
        var vm = CreateVm(out var apiMock);
        var securityId = Guid.NewGuid();
        SetupSecurity(apiMock, securityId);
        apiMock.Setup(x => x.Securities_UpdateAsync(securityId, It.IsAny<SecurityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityDto
            {
                Id = securityId,
                Name = "Changed",
                Identifier = "ISIN123",
                CurrencyCode = "EUR",
                IsActive = true
            });
        await vm.LoadAsync(securityId);

        var nameField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_Security_Name");
        vm.ValidateFieldValue(nameField, "Changed");
        Assert.True(vm.HasPendingChanges);

        var saved = await vm.SaveAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(saved);
        Assert.False(vm.HasPendingChanges);
        Assert.True(GetAction(vm, "Save").Disabled);
        Assert.True(GetAction(vm, "Reset").Disabled);
    }

    /// <summary>
    /// In new-create mode Reset must restore the initial state including the InitPrefill name
    /// and must not persist anything.
    /// </summary>
    [Fact]
    public async Task Reset_NewMode_RestoresPrefill()
    {
        var vm = CreateVm(out var apiMock);
        apiMock.Setup(x => x.SecurityCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SecurityCategoryDto>());
        vm.SetInitValue("PreETF");
        await vm.LoadAsync(Guid.Empty);

        var nameField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_Security_Name");
        Assert.Equal("PreETF", nameField.Text);
        vm.ValidateFieldValue(nameField, "Other");
        Assert.True(vm.HasPendingChanges);

        await GetAction(vm, "Reset").Callback!();

        Assert.False(vm.HasPendingChanges);
        Assert.Equal("PreETF", vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_Security_Name").Text);
        apiMock.Verify(x => x.Securities_CreateAsync(It.IsAny<SecurityRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        apiMock.Verify(x => x.Securities_GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static void SetupSecurity(Mock<IApiClient> apiMock, Guid securityId)
    {
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
    }

    private static UiRibbonTab GetTab(SecurityCardViewModel vm, string title)
        => vm.GetRibbon(new PassthroughLocalizer())!
            .SelectMany(x => x.Tabs ?? new List<UiRibbonTab>())
            .Single(t => t.Title == title);

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

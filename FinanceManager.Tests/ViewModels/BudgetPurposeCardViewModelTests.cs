using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Web.ViewModels.Budget;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers the "Zurücksetzen" ribbon action of <see cref="BudgetPurposeCardViewModel"/>: the
/// action sits directly behind Save in the manage group, is disabled without pending changes
/// and rebuilds the card record from the loaded <see cref="BudgetPurposeDto"/> without
/// re-fetching the entity.
/// </summary>
public sealed class BudgetPurposeCardViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private sealed class DummyLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private sealed class DummyLocalizerGeneric<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private static (BudgetPurposeCardViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(DummyLocalizerGeneric<>));
        var sp = services.BuildServiceProvider();
        return (new BudgetPurposeCardViewModel(sp), apiMock);
    }

    private static BudgetPurposeDto NewPurpose(Guid id)
        => new(id, Guid.NewGuid(), "Purpose A", "desc", BudgetSourceType.ContactGroup, Guid.NewGuid(), null);

    private static void SetupLoad(Mock<IApiClient> apiMock, Guid purposeId, BudgetPurposeDto dto)
    {
        apiMock.Setup(a => a.Budgets_GetPurposeAsync(purposeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        apiMock.Setup(a => a.Budgets_ListRulesByPurposeAsync(purposeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetRuleDto>());
        apiMock.Setup(a => a.ContactCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ContactCategoryDto>());
        apiMock.Setup(a => a.Budgets_ListCategoriesAsync(It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetCategoryOverviewDto>());
    }

    private static UiRibbonTab FindTab(BudgetPurposeCardViewModel vm, string title)
        => vm.GetRibbonRegisters(new DummyLocalizer())!
            .SelectMany(r => r.Tabs ?? Enumerable.Empty<UiRibbonTab>())
            .Single(t => t.Title == title);

    private static UiRibbonAction FindAction(BudgetPurposeCardViewModel vm, string actionId)
        => vm.GetRibbonRegisters(new DummyLocalizer())!
            .SelectMany(r => r.Tabs ?? Enumerable.Empty<UiRibbonTab>())
            .SelectMany(t => t.Items)
            .Single(a => a.Id == actionId);

    /// <summary>
    /// The "Zurücksetzen" action must sit in the manage group directly behind Save as a small
    /// item with the undo icon, no mobile shortcut and a disabled state while no pending
    /// changes exist.
    /// </summary>
    [Fact]
    public async Task Ribbon_ContainsReset_AfterSave_InManageGroup()
    {
        var (vm, apiMock) = CreateVm();
        var purposeId = Guid.NewGuid();
        SetupLoad(apiMock, purposeId, NewPurpose(purposeId));
        await vm.LoadAsync(purposeId);

        var manage = FindTab(vm, "Ribbon_Group_Manage");
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
    /// Reset discards the pending name edit and rebuilds the card record from the loaded
    /// purpose dto without re-fetching the entity.
    /// </summary>
    [Fact]
    public async Task Reset_DiscardsPendingChanges()
    {
        var (vm, apiMock) = CreateVm();
        var purposeId = Guid.NewGuid();
        SetupLoad(apiMock, purposeId, NewPurpose(purposeId));
        await vm.LoadAsync(purposeId);

        var nameField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_BudgetPurpose_Name");
        vm.ValidateFieldValue(nameField, "Changed");
        Assert.True(vm.HasPendingChanges);

        var reset = FindAction(vm, "Reset");
        Assert.False(reset.Disabled);
        await reset.Callback!();

        Assert.False(vm.HasPendingChanges);
        Assert.Equal("Purpose A", vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_BudgetPurpose_Name").Text);
        apiMock.Verify(a => a.Budgets_GetPurposeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Changing the source type clears the source-id field and switches its lookup type in the
    /// pending record; Reset must rebuild the card so the persisted source type's lookup
    /// configuration is restored.
    /// </summary>
    [Fact]
    public async Task Reset_RestoresSourceFieldConsistency()
    {
        var (vm, apiMock) = CreateVm();
        var purposeId = Guid.NewGuid();
        SetupLoad(apiMock, purposeId, NewPurpose(purposeId));
        await vm.LoadAsync(purposeId);

        var sourceTypeField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_BudgetPurpose_SourceType");
        vm.ValidateLookupField(sourceTypeField, new BaseViewModel.LookupItem(Guid.NewGuid(), nameof(BudgetSourceType.Contact)));

        var sourceIdField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_BudgetPurpose_SourceId");
        Assert.Equal("Contact", sourceIdField.LookupType);

        await FindAction(vm, "Reset").Callback!();

        var restored = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_BudgetPurpose_SourceId");
        Assert.Equal("ContactCategory", restored.LookupType);
        Assert.False(vm.HasPendingChanges);
    }

    /// <summary>
    /// In new-create mode Reset restores the initial dto including the InitPrefill name without
    /// any create/fetch API calls for the entity itself.
    /// </summary>
    [Fact]
    public async Task Reset_NewMode_RestoresPrefill()
    {
        var (vm, apiMock) = CreateVm();
        apiMock.Setup(a => a.ContactCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ContactCategoryDto>());
        apiMock.Setup(a => a.Budgets_ListCategoriesAsync(It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BudgetCategoryOverviewDto>());
        vm.SetInitValue("PrePurpose");
        await vm.LoadAsync(Guid.Empty);

        var nameField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_BudgetPurpose_Name");
        Assert.Equal("PrePurpose", nameField.Text);
        vm.ValidateFieldValue(nameField, "Other");
        Assert.True(vm.HasPendingChanges);

        await FindAction(vm, "Reset").Callback!();

        Assert.False(vm.HasPendingChanges);
        Assert.Equal("PrePurpose", vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_BudgetPurpose_Name").Text);
        apiMock.Verify(a => a.Budgets_GetPurposeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        apiMock.Verify(a => a.Budgets_CreatePurposeAsync(It.IsAny<BudgetPurposeCreateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Invoking the Reset callback while no purpose is loaded must not throw and must leave
    /// the card state untouched.
    /// </summary>
    [Fact]
    public async Task Reset_NullBaseline_DoesNotThrow()
    {
        var (vm, _) = CreateVm();

        await FindAction(vm, "Reset").Callback!();

        Assert.False(vm.HasPendingChanges);
    }
}

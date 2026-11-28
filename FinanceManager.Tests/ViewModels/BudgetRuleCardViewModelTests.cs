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
/// Covers the "Zurücksetzen" ribbon action of <see cref="BudgetRuleCardViewModel"/>: the
/// action sits directly behind Save in the manage group, is disabled without pending changes
/// and rebuilds the card record - including the interval-dependent editable state - from the
/// loaded <see cref="BudgetRuleDto"/> without re-fetching the entity.
/// </summary>
public sealed class BudgetRuleCardViewModelTests
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

    private static (BudgetRuleCardViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(DummyLocalizerGeneric<>));
        var sp = services.BuildServiceProvider();
        return (new BudgetRuleCardViewModel(sp), apiMock);
    }

    private static BudgetRuleDto NewRule(Guid ruleId)
        => new(ruleId, Guid.NewGuid(), Guid.NewGuid(), null, 42m, BudgetIntervalType.Monthly, null,
            new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), "Rent", false);

    private static UiRibbonTab FindTab(BudgetRuleCardViewModel vm, string title)
        => vm.GetRibbonRegisters(new DummyLocalizer())!
            .SelectMany(r => r.Tabs ?? Enumerable.Empty<UiRibbonTab>())
            .Single(t => t.Title == title);

    private static UiRibbonAction FindAction(BudgetRuleCardViewModel vm, string actionId)
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
        var ruleId = Guid.NewGuid();
        apiMock.Setup(a => a.Budgets_GetRuleAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewRule(ruleId));
        await vm.LoadAsync(ruleId);

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
    /// Reset discards pending edits and rebuilds the card record from the loaded rule dto
    /// without re-fetching the entity.
    /// </summary>
    [Fact]
    public async Task Reset_DiscardsPendingChanges()
    {
        var (vm, apiMock) = CreateVm();
        var ruleId = Guid.NewGuid();
        apiMock.Setup(a => a.Budgets_GetRuleAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewRule(ruleId));
        await vm.LoadAsync(ruleId);

        var patternField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_BudgetRule_PurposePattern");
        vm.ValidateFieldValue(patternField, "Changed");
        Assert.True(vm.HasPendingChanges);

        var reset = FindAction(vm, "Reset");
        Assert.False(reset.Disabled);
        await reset.Callback!();

        Assert.False(vm.HasPendingChanges);
        Assert.Equal("Rent", vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_BudgetRule_PurposePattern").Text);
        apiMock.Verify(a => a.Budgets_GetRuleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Switching the interval to CustomMonths enables the custom-months field. Reset must
    /// rebuild the card so the field is disabled and cleared again for the persisted
    /// Monthly interval.
    /// </summary>
    [Fact]
    public async Task Reset_RestoresIntervalDependentFieldState()
    {
        var (vm, apiMock) = CreateVm();
        var ruleId = Guid.NewGuid();
        apiMock.Setup(a => a.Budgets_GetRuleAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewRule(ruleId));
        await vm.LoadAsync(ruleId);

        var intervalField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_BudgetRule_Interval");
        vm.ValidateLookupField(intervalField, new BaseViewModel.LookupItem(Guid.NewGuid(), nameof(BudgetIntervalType.CustomMonths)));

        var customField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_BudgetRule_CustomIntervalMonths");
        Assert.True(customField.Editable);
        vm.ValidateFieldValue(customField, "3");
        Assert.True(vm.HasPendingChanges);

        await FindAction(vm, "Reset").Callback!();

        var restored = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_BudgetRule_CustomIntervalMonths");
        Assert.False(restored.Editable);
        Assert.Equal(string.Empty, restored.Text);
        Assert.False(vm.HasPendingChanges);
    }

    /// <summary>
    /// In new-create mode the initial dto is derived from the InitPrefill
    /// ("purpose:{guid}" / "category:{guid}"); Reset must restore that initial card state
    /// without any create/fetch API calls.
    /// </summary>
    [Fact]
    public async Task Reset_NewMode_RestoresPrefill()
    {
        var (vm, apiMock) = CreateVm();
        var purposeId = Guid.NewGuid();
        vm.SetInitValue($"purpose:{purposeId}");
        await vm.LoadAsync(Guid.Empty);

        Assert.Equal(purposeId, vm.BudgetPurposeId);
        var patternField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_BudgetRule_PurposePattern");
        Assert.Equal(string.Empty, patternField.Text);
        vm.ValidateFieldValue(patternField, "Other");
        Assert.True(vm.HasPendingChanges);

        await FindAction(vm, "Reset").Callback!();

        Assert.False(vm.HasPendingChanges);
        Assert.Equal(string.Empty, vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_BudgetRule_PurposePattern").Text);
        apiMock.Verify(a => a.Budgets_GetRuleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        apiMock.Verify(a => a.Budgets_CreateRuleAsync(It.IsAny<BudgetRuleCreateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Invoking the Reset callback while no rule is loaded must not throw.
    /// </summary>
    [Fact]
    public async Task Reset_NullBaseline_DoesNotThrow()
    {
        var (vm, _) = CreateVm();

        await FindAction(vm, "Reset").Callback!();

        Assert.False(vm.HasPendingChanges);
    }
}

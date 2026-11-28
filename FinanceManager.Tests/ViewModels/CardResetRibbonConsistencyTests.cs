using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Guards the agreed ribbon shape of the "Zurücksetzen" action across every card detail view
/// model that offers it: the action must sit in <c>Ribbon_Group_Manage</c> directly behind
/// <c>Save</c>, rendered small with the undo icon and the localized <c>Ribbon_Reset</c>
/// label, and must not be exposed as a mobile shortcut (that stays exclusive to <c>Save</c>).
/// The reference implementation <c>BankAccountCardViewModel</c> is covered as well so all
/// card reset actions stay aligned.
/// </summary>
public sealed class CardResetRibbonConsistencyTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private sealed class PassthroughLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private sealed class PassthroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton(new Mock<IApiClient>().Object);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Every card view model that exposes the discard-changes ribbon action.
    /// The factories only construct the view model - no data is loaded - so the
    /// ribbon shape assertions run against the freshly initialized state where
    /// <c>HasPendingChanges</c> is <c>false</c>.
    /// </summary>
    public static IEnumerable<object[]> CardViewModels
    {
        get
        {
            yield return new object[] { nameof(FinanceManager.Web.ViewModels.Accounts.BankAccountCardViewModel), (Func<IServiceProvider, BaseViewModel>)(sp => new FinanceManager.Web.ViewModels.Accounts.BankAccountCardViewModel(sp)) };
            yield return new object[] { nameof(FinanceManager.Web.ViewModels.Contacts.ContactCardViewModel), (Func<IServiceProvider, BaseViewModel>)(sp => new FinanceManager.Web.ViewModels.Contacts.ContactCardViewModel(sp)) };
            yield return new object[] { nameof(FinanceManager.Web.ViewModels.SavingsPlans.SavingsPlanCardViewModel), (Func<IServiceProvider, BaseViewModel>)(sp => new FinanceManager.Web.ViewModels.SavingsPlans.SavingsPlanCardViewModel(sp)) };
            yield return new object[] { nameof(FinanceManager.Web.ViewModels.SavingsPlans.Categories.SavingsPlanCategoryCardViewModel), (Func<IServiceProvider, BaseViewModel>)(sp => new FinanceManager.Web.ViewModels.SavingsPlans.Categories.SavingsPlanCategoryCardViewModel(sp)) };
            yield return new object[] { nameof(FinanceManager.Web.ViewModels.Securities.SecurityCardViewModel), (Func<IServiceProvider, BaseViewModel>)(sp => new FinanceManager.Web.ViewModels.Securities.SecurityCardViewModel(sp)) };
            yield return new object[] { nameof(FinanceManager.Web.ViewModels.Securities.Categories.SecurityCategoryCardViewModel), (Func<IServiceProvider, BaseViewModel>)(sp => new FinanceManager.Web.ViewModels.Securities.Categories.SecurityCategoryCardViewModel(sp)) };
            yield return new object[] { nameof(FinanceManager.Web.ViewModels.Budget.BudgetPurposeCardViewModel), (Func<IServiceProvider, BaseViewModel>)(sp => new FinanceManager.Web.ViewModels.Budget.BudgetPurposeCardViewModel(sp)) };
            yield return new object[] { nameof(FinanceManager.Web.ViewModels.Budget.BudgetRuleCardViewModel), (Func<IServiceProvider, BaseViewModel>)(sp => new FinanceManager.Web.ViewModels.Budget.BudgetRuleCardViewModel(sp)) };
            yield return new object[] { nameof(FinanceManager.Web.ViewModels.Mcp.McpAccessCardViewModel), (Func<IServiceProvider, BaseViewModel>)(sp => new FinanceManager.Web.ViewModels.Mcp.McpAccessCardViewModel(sp, sp.GetRequiredService<IApiClient>())) };
        }
    }

    /// <summary>
    /// Asserts the uniform ribbon shape: <c>Reset</c> directly behind <c>Save</c> in the
    /// manage group, small, undo icon, localized label, disabled without pending changes
    /// and no mobile shortcut.
    /// </summary>
    /// <param name="viewModelName">Display name of the card view model under test, used in assertion messages.</param>
    /// <param name="factory">Factory that creates the view model instance from a service provider without loading data.</param>
    [Theory]
    [MemberData(nameof(CardViewModels))]
    public void ResetAction_HasAgreedRibbonShape(string viewModelName, Func<IServiceProvider, BaseViewModel> factory)
    {
        var vm = factory(CreateServiceProvider());

        var registers = vm.GetRibbonRegisters(new PassthroughLocalizer());
        Assert.NotNull(registers);

        var manage = registers!
            .SelectMany(r => r.Tabs ?? Enumerable.Empty<UiRibbonTab>())
            .FirstOrDefault(t => t.Title == "Ribbon_Group_Manage");
        Assert.True(manage != null, $"{viewModelName}: manage ribbon group not found");

        var items = manage!.Items;
        var saveIndex = items.FindIndex(i => i.Id == "Save");
        Assert.True(saveIndex >= 0, $"{viewModelName}: Save action not found in manage group");

        var resetIndex = items.FindIndex(i => i.Id == "Reset");
        Assert.Equal(saveIndex + 1, resetIndex);

        var reset = items[resetIndex];
        Assert.Equal(UiRibbonItemSize.Small, reset.Size);
        Assert.Contains("#undo", reset.IconSvg);
        Assert.Equal("Ribbon_Reset", reset.Label);
        Assert.False(reset.MobileShortcut);
        Assert.True(reset.Disabled);
        Assert.False(reset.Hidden);
        Assert.NotNull(reset.Callback);
    }
}

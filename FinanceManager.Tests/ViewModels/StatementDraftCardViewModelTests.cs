using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.StatementDrafts;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;
using System.Linq;

namespace FinanceManager.Tests.ViewModels;

public sealed class StatementDraftCardViewModelTests
{
    private sealed class DummyGenericLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private sealed class DummyLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private static (StatementDraftCardViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(DummyGenericLocalizer<>));
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        var sp = services.BuildServiceProvider();
        var vm = new StatementDraftCardViewModel(sp);
        return (vm, apiMock);
    }

    [Fact]
    public async Task NewDraft_SelectingBankAccount_enablesSaveInRibbon()
    {
        // Arrange
        var (vm, apiMock) = CreateVm();
        // Act: initialize in create/new mode
        await vm.LoadAsync(System.Guid.Empty);

        // It's possible the CardRecord isn't populated in this unit test environment.
        // Construct a minimal CardField for the assigned account and simulate selection.
        var acctField = new CardField(
            "Card_Caption_StatementDrafts_AssignedAccount",
            CardFieldKind.Text,
            text: string.Empty,
            symbolId: null,
            amount: null,
            boolValue: null,
            editable: true,
            lookupType: "bankaccount",
            lookupField: "Name",
            valueId: null,
            lookupFilter: null,
            hint: null,
            allowAdd: true);

        // simulate user selecting a bank account lookup item
        var lookup = new BaseViewModel.LookupItem(System.Guid.NewGuid(), "Test Account");
        vm.ValidateLookupField(acctField, lookup);

        // Selecting a lookup should create a pending change
        Assert.True(vm.HasPendingChanges, "ViewModel must report pending changes after selecting a lookup item.");

        // Request ribbon registers (use GetRibbonRegisters to ensure aggregated registers are returned)
        var localizer = new DummyLocalizer();
        var regs = ((FinanceManager.Web.ViewModels.Common.BaseViewModel)vm).GetRibbonRegisters(localizer);

        // In this test environment we expect ribbon registers to be present and include the Save action.
        Assert.NotNull(regs);
        var saveAction = regs.SelectMany(r => r.Tabs ?? Enumerable.Empty<FinanceManager.Web.ViewModels.Common.UiRibbonTab>())
            .SelectMany(t => t.Items ?? Enumerable.Empty<FinanceManager.Web.ViewModels.Common.UiRibbonAction>())
            .FirstOrDefault(a => a.Action == "Save");

        Assert.NotNull(saveAction);
        Assert.False(saveAction.Disabled, "Save action must be enabled after selecting an account in create mode");
    }
}

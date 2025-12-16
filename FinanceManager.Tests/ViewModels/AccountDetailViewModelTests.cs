using FinanceManager.Application;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using System.Reflection;

namespace FinanceManager.Tests.ViewModels;

public sealed class AccountDetailViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId => Guid.NewGuid();
        public string? PreferredLanguage => "de";
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin => false;
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
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => (IStringLocalizer)this;
    }

    private static (FinanceManager.Web.ViewModels.Accounts.BankAccountCardViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        var currentUser = new TestCurrentUserService { IsAuthenticated = true };
        services.AddSingleton<ICurrentUserService>(currentUser);
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        // register a generic IStringLocalizer<Pages> required by BaseViewModel
        services.AddSingleton(typeof(IStringLocalizer<>).MakeGenericType(typeof(FinanceManager.Web.Pages)), new DummyLocalizerGeneric<FinanceManager.Web.Pages>());
        var sp = services.BuildServiceProvider();
        var vm = new FinanceManager.Web.ViewModels.Accounts.BankAccountCardViewModel(sp);
        return (vm, apiMock);
    }

    [Fact]
    public async Task Initialize_LoadsAccount_and_builds_card()
    {
        var accountId = Guid.NewGuid();
        var bankContactId = Guid.NewGuid();
        var (vm, apiMock) = CreateVm();

        apiMock.Setup(a => a.GetAccountAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceManager.Shared.Dtos.Accounts.AccountDto(accountId, "My Account", FinanceManager.Shared.Dtos.Accounts.AccountType.Giro, "DE00", 0m, bankContactId, null, FinanceManager.Shared.Dtos.Accounts.SavingsPlanExpectation.Optional));

        apiMock.Setup(a => a.Contacts_GetAsync(bankContactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactDto(bankContactId, "Bank A", ContactType.Bank, null, null, false, null));

        await vm.InitializeAsync(accountId);

        Assert.NotNull(vm.Account);
        Assert.Equal(accountId, vm.Id);
        Assert.Equal("My Account", vm.Account!.Name);
        Assert.Equal(FinanceManager.Shared.Dtos.Accounts.AccountType.Giro, vm.Account.Type);
        Assert.Equal("DE00", vm.Account.Iban);
        Assert.Equal("My Account", vm.Title);
        Assert.NotNull(vm.CardRecord);
    }

    [Fact]
    public async Task Initialize_NewAccount_ProducesEmptyCard()
    {
        var (vm, apiMock) = CreateVm();

        await vm.InitializeAsync(Guid.Empty);

        Assert.Equal(Guid.Empty, vm.Id);
        Assert.NotNull(vm.Account);
        Assert.NotNull(vm.CardRecord);
    }

    [Fact]
    public async Task Save_NewAccount_CreatesAndReturnsId()
    {
        var createdId = Guid.NewGuid();
        var (vm, apiMock) = CreateVm();

        apiMock.Setup(a => a.CreateAccountAsync(It.IsAny<FinanceManager.Shared.Dtos.Accounts.AccountCreateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceManager.Shared.Dtos.Accounts.AccountDto(createdId, "Created", FinanceManager.Shared.Dtos.Accounts.AccountType.Savings, null, 0m, Guid.NewGuid(), null, FinanceManager.Shared.Dtos.Accounts.SavingsPlanExpectation.Optional));

        // Initialize for new account
        await vm.InitializeAsync(Guid.Empty);

        // set pending name to trigger save
        var nameField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_Account_Name");
        vm.ValidateFieldValue(nameField, "New");

        // call private SavePendingAsync via reflection
        var mi = typeof(FinanceManager.Web.ViewModels.Accounts.BankAccountCardViewModel).GetMethod("SavePendingAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)mi.Invoke(vm, Array.Empty<object>())!;
        await task;

        Assert.Equal(createdId, vm.Id);
        Assert.NotNull(vm.Account);
        Assert.Equal(createdId, vm.Account!.Id);
    }

    [Fact]
    public async Task Save_Update_CallsUpdateAndKeepsId()
    {
        var accountId = Guid.NewGuid();
        var (vm, apiMock) = CreateVm();

        apiMock.Setup(a => a.GetAccountAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceManager.Shared.Dtos.Accounts.AccountDto(accountId, "Existing", FinanceManager.Shared.Dtos.Accounts.AccountType.Giro, null, 0m, Guid.NewGuid(), null, FinanceManager.Shared.Dtos.Accounts.SavingsPlanExpectation.Optional));

        apiMock.Setup(a => a.UpdateAccountAsync(accountId, It.IsAny<FinanceManager.Shared.Dtos.Accounts.AccountUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceManager.Shared.Dtos.Accounts.AccountDto(accountId, "ExistingUpdated", FinanceManager.Shared.Dtos.Accounts.AccountType.Giro, null, 0m, Guid.NewGuid(), null, FinanceManager.Shared.Dtos.Accounts.SavingsPlanExpectation.Optional));

        await vm.InitializeAsync(accountId);

        var nameField = vm.CardRecord!.Fields.First(f => f.LabelKey == "Card_Caption_Account_Name");
        vm.ValidateFieldValue(nameField, "ExistingUpdated");

        var mi = typeof(FinanceManager.Web.ViewModels.Accounts.BankAccountCardViewModel).GetMethod("SavePendingAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)mi.Invoke(vm, Array.Empty<object>())!;
        await task;

        apiMock.Verify(a => a.UpdateAccountAsync(accountId, It.IsAny<FinanceManager.Shared.Dtos.Accounts.AccountUpdateRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(accountId, vm.Id);
        Assert.Equal("ExistingUpdated", vm.Account!.Name);
    }

    [Fact]
    public async Task DeleteAsync_Success_ReturnsTrue()
    {
        var accountId = Guid.NewGuid();
        var (vm, apiMock) = CreateVm();

        apiMock.Setup(a => a.GetAccountAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceManager.Shared.Dtos.Accounts.AccountDto(accountId, "ToDelete", FinanceManager.Shared.Dtos.Accounts.AccountType.Giro, null, 0m, Guid.NewGuid(), null, FinanceManager.Shared.Dtos.Accounts.SavingsPlanExpectation.Optional));

        apiMock.Setup(a => a.DeleteAccountAsync(accountId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await vm.InitializeAsync(accountId);
        var ok = await vm.DeleteAsync();

        Assert.True(ok);
    }

    [Fact]
    public void GetRibbon_ContainsExpectedGroups()
    {
        var (vm, _) = CreateVm();
        var loc = new DummyLocalizer();

        // New account (empty id)
        var groupsNew = vm.GetRibbon(loc);
        Assert.Contains(groupsNew, g => g.Tabs != null && g.Tabs.Any(t => t.Title == "Ribbon_Group_Navigation"));
        Assert.Contains(groupsNew, g => g.Tabs != null && g.Tabs.Any(t => t.Title == "Ribbon_Group_Manage"));
        Assert.Contains(groupsNew, g => g.Tabs != null && g.Tabs.Any(t => t.Title == "Ribbon_Group_Linked"));

        // Existing account: still contains linked group
        // simulate by initializing
        var accountId = Guid.NewGuid();
        // We don't need to load account for ribbon shape
        var groupsExisting = vm.GetRibbon(loc);
        Assert.Contains(groupsExisting, g => g.Tabs != null && g.Tabs.Any(t => t.Title == "Ribbon_Group_Linked"));
    }
}

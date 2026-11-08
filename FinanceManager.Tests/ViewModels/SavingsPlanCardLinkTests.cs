using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.SavingsPlans;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <see cref="SavingsPlanCardViewModel"/>'s account-link management (load/add/remove)
/// and the actual-balance report ("Istmeldung") action: the report stays available only for
/// plans without linked accounts and the ribbon action reflects that state.
/// </summary>
public sealed class SavingsPlanCardLinkTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private sealed class PassthroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) { yield break; }
    }

    private sealed class DummyLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    private static (SavingsPlanCardViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));
        var sp = services.BuildServiceProvider();
        return (new SavingsPlanCardViewModel(sp), apiMock);
    }

    private static SavingsPlanDto NewPlan(Guid id)
        => new(id, "Plan A", SavingsPlanType.OneTime, 1000m, DateTime.Today.AddMonths(6),
            null, true, DateTime.UtcNow, null, null);

    private static AccountDto NewAccount(Guid id, string name = "acct")
        => new(id, name, AccountType.Savings, "DE00" + id.ToString("N")[..6], 100m,
            Guid.NewGuid(), null, SavingsPlanExpectation.Optional, true);

    private static void SetupLoad(Mock<IApiClient> apiMock, Guid planId, IReadOnlyList<Guid> linkedIds, IReadOnlyList<AccountDto> accounts)
    {
        apiMock.Setup(a => a.SavingsPlans_GetAsync(planId, It.IsAny<CancellationToken>())).ReturnsAsync(NewPlan(planId));
        apiMock.Setup(a => a.SavingsPlans_AnalyzeAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsPlanAnalysisDto(planId, true, 1000m, DateTime.Today.AddMonths(6), 100m, 50m, 6));
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());
        apiMock.Setup(a => a.SavingsPlans_GetLinkedAccountsAsync(planId, It.IsAny<CancellationToken>())).ReturnsAsync(linkedIds);
        apiMock.Setup(a => a.GetAccountsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accounts);
    }

    private static UiRibbonAction? FindActualReportAction(SavingsPlanCardViewModel vm)
        => vm.GetRibbonRegisters(new DummyLocalizer())?
            .SelectMany(r => r.Tabs ?? Enumerable.Empty<UiRibbonTab>())
            .SelectMany(t => t.Items ?? Enumerable.Empty<UiRibbonAction>())
            .SingleOrDefault(a => a.Id == "ActualReport");

    /// <summary>
    /// Loading a plan resolves its linked account ids against the owner's accounts and
    /// offers the remaining accounts as linkable.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ResolvesLinkedAndLinkableAccounts()
    {
        var (vm, apiMock) = CreateVm();
        var planId = Guid.NewGuid();
        var linked = NewAccount(Guid.NewGuid(), "bsv");
        var other = NewAccount(Guid.NewGuid(), "giro");
        SetupLoad(apiMock, planId, new[] { linked.Id }, new[] { linked, other });

        await vm.LoadAsync(planId);

        vm.LinkedAccounts.Should().ContainSingle(a => a.Id == linked.Id);
        vm.LinkableAccounts.Should().ContainSingle(a => a.Id == other.Id);
        vm.HasLinkedAccounts.Should().BeTrue();
        vm.CanReportActualBalance.Should().BeFalse();
    }

    /// <summary>
    /// Only savings accounts are offered for linking - giro and credit-card accounts are
    /// filtered out of the linkable list while a savings account remains selectable.
    /// </summary>
    [Fact]
    public async Task LoadAsync_NonSavingsAccounts_NotLinkable()
    {
        var (vm, apiMock) = CreateVm();
        var planId = Guid.NewGuid();
        var savings = NewAccount(Guid.NewGuid(), "spar");
        var giro = NewAccount(Guid.NewGuid(), "giro") with { Type = AccountType.Giro };
        var card = NewAccount(Guid.NewGuid(), "karte") with { Type = AccountType.CreditCard };
        SetupLoad(apiMock, planId, Array.Empty<Guid>(), new[] { savings, giro, card });

        await vm.LoadAsync(planId);

        vm.LinkableAccounts.Should().ContainSingle(a => a.Id == savings.Id);
        vm.LinkableAccounts.Should().NotContain(a => a.Type == AccountType.Giro);
        vm.LinkableAccounts.Should().NotContain(a => a.Type == AccountType.CreditCard);
    }

    /// <summary>
    /// Opening a linked account raises the OpenAccountDetails navigation action.
    /// </summary>
    [Fact]
    public async Task OpenLinkedAccount_RaisesOpenAccountDetails()
    {
        var (vm, apiMock) = CreateVm();
        var planId = Guid.NewGuid();
        var linked = NewAccount(Guid.NewGuid(), "bsv");
        SetupLoad(apiMock, planId, new[] { linked.Id }, new[] { linked });
        await vm.LoadAsync(planId);
        string? action = null;
        string? param = null;
        vm.UiActionRequested += (_, e) => { action = e?.Action; param = e?.Payload; };

        vm.OpenLinkedAccount(linked.Id);

        action.Should().Be("OpenAccountDetails");
        param.Should().Be(linked.Id.ToString());
    }

    /// <summary>
    /// Adding the selected account calls the link endpoint and refreshes the link state
    /// and the analysis (account-based accumulated amount).
    /// </summary>
    [Fact]
    public async Task AddAccountLinkAsync_CallsApi_AndRefreshesState()
    {
        var (vm, apiMock) = CreateVm();
        var planId = Guid.NewGuid();
        var account = NewAccount(Guid.NewGuid());
        SetupLoad(apiMock, planId, Array.Empty<Guid>(), new[] { account });
        await vm.LoadAsync(planId);

        vm.NewLinkedAccountId = account.Id;
        apiMock.Setup(a => a.SavingsPlans_AddAccountLinkAsync(planId, account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        apiMock.Setup(a => a.SavingsPlans_GetLinkedAccountsAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { account.Id });

        var ok = await vm.AddAccountLinkAsync(TestContext.Current.CancellationToken);

        ok.Should().BeTrue();
        apiMock.Verify(a => a.SavingsPlans_AddAccountLinkAsync(planId, account.Id, It.IsAny<CancellationToken>()), Times.Once);
        vm.LinkedAccounts.Should().ContainSingle(a => a.Id == account.Id);
        vm.NewLinkedAccountId.Should().BeNull();
    }

    /// <summary>
    /// Removing a link calls the unlink endpoint; once the last link is gone the
    /// actual-balance report becomes available again.
    /// </summary>
    [Fact]
    public async Task RemoveAccountLinkAsync_CallsApi_AndRestoresReportEligibility()
    {
        var (vm, apiMock) = CreateVm();
        var planId = Guid.NewGuid();
        var account = NewAccount(Guid.NewGuid());
        SetupLoad(apiMock, planId, new[] { account.Id }, new[] { account });
        await vm.LoadAsync(planId);
        vm.CanReportActualBalance.Should().BeFalse();

        apiMock.Setup(a => a.SavingsPlans_RemoveAccountLinkAsync(planId, account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        apiMock.Setup(a => a.SavingsPlans_GetLinkedAccountsAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        await vm.RemoveAccountLinkAsync(account.Id, TestContext.Current.CancellationToken);

        apiMock.Verify(a => a.SavingsPlans_RemoveAccountLinkAsync(planId, account.Id, It.IsAny<CancellationToken>()), Times.Once);
        vm.HasLinkedAccounts.Should().BeFalse();
        vm.CanReportActualBalance.Should().BeTrue();
    }

    /// <summary>
    /// The "Istmeldung" ribbon action is enabled for a plan without links and disabled
    /// (with explanatory tooltip) once accounts are linked.
    /// </summary>
    [Fact]
    public async Task Ribbon_ActualReport_EnabledOnlyWithoutLinks()
    {
        var (vm, apiMock) = CreateVm();
        var planId = Guid.NewGuid();
        var account = NewAccount(Guid.NewGuid());
        SetupLoad(apiMock, planId, Array.Empty<Guid>(), new[] { account });
        await vm.LoadAsync(planId);

        var enabled = FindActualReportAction(vm);
        enabled.Should().NotBeNull();
        enabled!.Disabled.Should().BeFalse();
        enabled.Tooltip.Should().BeNull();

        apiMock.Setup(a => a.SavingsPlans_GetLinkedAccountsAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { account.Id });
        await vm.LoadLinkedAccountsAsync(TestContext.Current.CancellationToken);

        var disabled = FindActualReportAction(vm);
        disabled!.Disabled.Should().BeTrue();
        disabled.Tooltip.Should().NotBeNull();
    }

    /// <summary>
    /// Reporting the actual balance posts the reported value to the endpoint and reloads
    /// the card so the corrected accumulated amount shows up.
    /// </summary>
    [Fact]
    public async Task ReportActualBalanceAsync_PostsReport_AndReloads()
    {
        var (vm, apiMock) = CreateVm();
        var planId = Guid.NewGuid();
        SetupLoad(apiMock, planId, Array.Empty<Guid>(), Array.Empty<AccountDto>());
        await vm.LoadAsync(planId);

        apiMock.Setup(a => a.SavingsPlans_ReportActualBalanceAsync(
                planId, It.Is<SavingsPlanActualReportRequest>(r => r.ReportedBalance == 50m), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsPlanActualReportResponse(Guid.NewGuid(), 75.30m, 50m));

        var ok = await vm.ReportActualBalanceAsync(50m, TestContext.Current.CancellationToken);

        ok.Should().BeTrue();
        apiMock.Verify(a => a.SavingsPlans_ReportActualBalanceAsync(
            planId, It.IsAny<SavingsPlanActualReportRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        apiMock.Verify(a => a.SavingsPlans_GetAsync(planId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>
    /// A linked plan rejects the report locally without calling the endpoint — the
    /// account balance is the single source of truth.
    /// </summary>
    [Fact]
    public async Task ReportActualBalanceAsync_LinkedPlan_RejectedLocally()
    {
        var (vm, apiMock) = CreateVm();
        var planId = Guid.NewGuid();
        var account = NewAccount(Guid.NewGuid());
        SetupLoad(apiMock, planId, new[] { account.Id }, new[] { account });
        await vm.LoadAsync(planId);

        var ok = await vm.ReportActualBalanceAsync(50m, TestContext.Current.CancellationToken);

        ok.Should().BeFalse();
        vm.LastError.Should().NotBeNullOrEmpty();
        apiMock.Verify(a => a.SavingsPlans_ReportActualBalanceAsync(
            It.IsAny<Guid>(), It.IsAny<SavingsPlanActualReportRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

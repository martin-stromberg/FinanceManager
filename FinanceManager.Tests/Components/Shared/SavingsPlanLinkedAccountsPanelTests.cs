using Bunit;
using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.ViewModels.SavingsPlans;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Components.Shared;

/// <summary>
/// Tests for <see cref="SavingsPlanLinkedAccountsPanel"/>: the linked-account rows must offer
/// the unlink action for every link — including the single-link layout that only shows the
/// icon buttons (no balance column).
/// </summary>
public sealed class SavingsPlanLinkedAccountsPanelTests : BunitContext
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

    private static (SavingsPlanCardViewModel vm, Mock<IApiClient> apiMock) CreateVm(Mock<FinanceManager.Web.Services.IConfirmationService>? confirmations = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));
        services.AddSingleton(confirmations?.Object ?? new Mock<FinanceManager.Web.Services.IConfirmationService>().Object);
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

    /// <summary>
    /// With exactly one linked account the row keeps its icon-only layout but must still offer
    /// the unlink button next to the open-account button — a single link must be removable.
    /// </summary>
    [Fact]
    public async Task SingleLink_RendersOpenAndRemoveButtons()
    {
        var (vm, apiMock) = CreateVm();
        var planId = Guid.NewGuid();
        var account = NewAccount(Guid.NewGuid(), "bsv");
        SetupLoad(apiMock, planId, new[] { account.Id }, new[] { account });
        await vm.LoadAsync(planId);

        var cut = Render<SavingsPlanLinkedAccountsPanel>(b => b.Add(c => c.Vm, vm));

        var accountRow = cut.FindAll("tbody tr").First(r => r.TextContent.Contains("bsv"));
        var buttons = accountRow.QuerySelectorAll("button.icon-btn");
        // open-account + unlink icon buttons (no balance column in the single-link layout)
        Assert.Equal(2, buttons.Length);
        Assert.NotNull(buttons.FirstOrDefault(b => b.GetAttribute("title") == "Unlink"));
        Assert.NotNull(buttons.FirstOrDefault(b => b.GetAttribute("title") == "Open account"));
    }

    /// <summary>
    /// Clicking the unlink button of the single linked account delegates to the view model's
    /// confirmed remove flow (API call after the confirmation service agrees).
    /// </summary>
    [Fact]
    public async Task SingleLink_ClickRemove_InvokesConfirmedRemove()
    {
        var confirmations = new Mock<FinanceManager.Web.Services.IConfirmationService>();
        confirmations.Setup(c => c.ConfirmAsync(It.IsAny<FinanceManager.Web.Services.ConfirmationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var (vm, apiMock) = CreateVm(confirmations);
        var planId = Guid.NewGuid();
        var account = NewAccount(Guid.NewGuid(), "bsv");
        SetupLoad(apiMock, planId, new[] { account.Id }, new[] { account });
        await vm.LoadAsync(planId);

        var cut = Render<SavingsPlanLinkedAccountsPanel>(b => b.Add(c => c.Vm, vm));

        apiMock.Setup(a => a.SavingsPlans_RemoveAccountLinkAsync(planId, account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        apiMock.Setup(a => a.SavingsPlans_GetLinkedAccountsAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        var unlink = cut.FindAll("button.icon-btn").First(b => b.GetAttribute("title") == "Unlink");
        await cut.InvokeAsync(() => unlink.Click());

        apiMock.Verify(a => a.SavingsPlans_RemoveAccountLinkAsync(planId, account.Id, It.IsAny<CancellationToken>()), Times.Once);
        vm.LinkedAccounts.Should().BeEmpty();
    }

    /// <summary>
    /// With several linked accounts each row keeps the balance column plus both icon buttons.
    /// </summary>
    [Fact]
    public async Task MultipleLinks_RenderBalanceAndBothButtons()
    {
        var (vm, apiMock) = CreateVm();
        var planId = Guid.NewGuid();
        var account1 = NewAccount(Guid.NewGuid(), "bsv1");
        var account2 = NewAccount(Guid.NewGuid(), "bsv2");
        SetupLoad(apiMock, planId, new[] { account1.Id, account2.Id }, new[] { account1, account2 });
        await vm.LoadAsync(planId);

        var cut = Render<SavingsPlanLinkedAccountsPanel>(b => b.Add(c => c.Vm, vm));

        var row = cut.FindAll("tbody tr").First(r => r.TextContent.Contains("bsv1"));
        Assert.Equal(2, row.QuerySelectorAll("button.icon-btn").Length);
        // balance column rendered for the multi-link layout
        Assert.Contains("100", row.TextContent);
    }
}

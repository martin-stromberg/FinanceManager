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
/// Covers the "NewRule" ribbon action of the embedded <see cref="BudgetRuleListViewModel"/> (#609):
/// without a valid purpose/category context (unsaved budget purpose card) the action must be
/// disabled and must not throw, while saved contexts keep the purpose/category payload navigation.
/// </summary>
public sealed class BudgetRuleListNewActionTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private sealed class TestLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) { yield break; }
    }

    private static IServiceProvider CreateSp(Mock<IApiClient> apiMock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton(apiMock.Object);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(TestLocalizer<>));
        return services.BuildServiceProvider();
    }

    private static Mock<IApiClient> CreateApiMock()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Budgets_ListRulesByPurposeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetRuleDto>());
        apiMock.Setup(a => a.Budgets_ListRulesByCategoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BudgetRuleDto>());
        return apiMock;
    }

    private static UiRibbonAction FindNewRuleAction(BudgetRuleListViewModel vm, IServiceProvider sp)
    {
        var loc = sp.GetRequiredService<IStringLocalizer<BudgetRuleListNewActionTests>>();
        return (vm.GetRibbonRegisters(loc) ?? Array.Empty<UiRibbonRegister>())
            .SelectMany(r => r.Tabs ?? new List<UiRibbonTab>())
            .SelectMany(t => t.Items)
            .Single(i => i.Id == "NewRule");
    }

    /// <summary>
    /// For an unsaved budget purpose (Guid.Empty, no category) the NewRule action must be disabled
    /// and invoking its callback must neither throw nor raise a UI action (#609 regression).
    /// </summary>
    [Fact]
    public async Task NewRule_UnsavedPurpose_IsDisabledAndSafe()
    {
        var apiMock = CreateApiMock();
        var sp = CreateSp(apiMock);
        var vm = new BudgetRuleListViewModel(sp);

        await vm.InitializeForPurposeAsync(Guid.Empty);

        var action = FindNewRuleAction(vm, sp);
        Assert.True(action.Disabled);

        string? raised = null;
        object? payload = null;
        vm.UiActionRequested += (_, e) => { raised = e?.Action; payload = e?.Payload; };

        await action.Callback!();

        Assert.Null(raised);
        Assert.Null(payload);
    }

    /// <summary>
    /// For a saved budget purpose the action stays enabled and raises the purpose payload.
    /// </summary>
    [Fact]
    public async Task NewRule_SavedPurpose_EnabledWithPurposePayload()
    {
        var apiMock = CreateApiMock();
        var sp = CreateSp(apiMock);
        var vm = new BudgetRuleListViewModel(sp);
        var purposeId = Guid.NewGuid();

        await vm.InitializeForPurposeAsync(purposeId);

        var action = FindNewRuleAction(vm, sp);
        Assert.False(action.Disabled);

        string? raised = null;
        object? payload = null;
        vm.UiActionRequested += (_, e) => { raised = e?.Action; payload = e?.Payload; };

        await action.Callback!();

        Assert.Equal("NewRule", raised);
        Assert.Equal($"purpose,{purposeId}", payload);
    }

    /// <summary>
    /// For a budget category the action raises the category payload.
    /// </summary>
    [Fact]
    public async Task NewRule_Category_EnabledWithCategoryPayload()
    {
        var apiMock = CreateApiMock();
        var sp = CreateSp(apiMock);
        var vm = new BudgetRuleListViewModel(sp);
        var categoryId = Guid.NewGuid();

        await vm.InitializeForCategoryAsync(categoryId);

        var action = FindNewRuleAction(vm, sp);
        Assert.False(action.Disabled);

        string? raised = null;
        object? payload = null;
        vm.UiActionRequested += (_, e) => { raised = e?.Action; payload = e?.Payload; };

        await action.Callback!();

        Assert.Equal("NewRule", raised);
        Assert.Equal($"category,{categoryId}", payload);
    }
}

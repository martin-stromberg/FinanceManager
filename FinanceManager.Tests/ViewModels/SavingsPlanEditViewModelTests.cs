using FinanceManager.Application;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.SavingsPlans;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <see cref="SavingsPlanCardViewModel"/>'s edit/create/save/delete lifecycle plus the derived,
/// read-only card fields it exposes based on the plan's analysis result: current and remaining amount, and
/// the conditional "required monthly" figure that only appears for one-time plans with a future target date
/// and a non-zero remaining amount (never for recurring plans, an already-reached target, or a fully funded plan).
/// </summary>
public sealed class SavingsPlanEditViewModelTests
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

    private static (SavingsPlanCardViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));
        var sp = services.BuildServiceProvider();
        var vm = new SavingsPlanCardViewModel(sp);
        return (vm, apiMock);
    }

    private static SavingsPlanDto CreateSavingsPlanDto(
        Guid id,
        SavingsPlanType type = SavingsPlanType.OneTime,
        decimal? targetAmount = 1000m,
        DateTime? targetDate = null,
        decimal currentAmount = 250m,
        decimal remainingAmount = 750m)
    {
        return new SavingsPlanDto(
            id,
            "Plan A",
            type,
            targetAmount,
            targetDate ?? DateTime.Today.AddMonths(6),
            type == SavingsPlanType.Recurring ? SavingsPlanInterval.Monthly : null,
            true,
            DateTime.UtcNow,
            null,
            null,
            remainingAmount: remainingAmount,
            currentAmount: currentAmount);
    }

    private static CardField? FindField(SavingsPlanCardViewModel vm, string labelKey)
    {
        return vm.CardRecord?.Fields.FirstOrDefault(f => f.LabelKey == labelKey);
    }

    /// <summary>
    /// Verifies that initializing with an existing plan id loads it in edit mode with the plan's data bound.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_Loads_Edit()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var dto = new SavingsPlanDto(id, "Plan A", SavingsPlanType.OneTime, 100m, DateTime.UtcNow.Date.AddMonths(6), null, true, DateTime.UtcNow, null, null, null);
        apiMock.Setup(a => a.SavingsPlans_GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        apiMock.Setup(a => a.SavingsPlans_AnalyzeAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new SavingsPlanAnalysisDto(id, true, 100m, DateTime.UtcNow.Date.AddMonths(6), 50m, 10m, 6));
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());

        await vm.InitializeAsync(id);

        Assert.True(vm.IsEdit);
        Assert.True(vm.Loaded);
        Assert.Equal("Plan A", vm.Model.Name);
    }

    /// <summary>
    /// Verifies that initializing a new plan (empty id) with an externally supplied init value pre-fills
    /// the plan's name from that value, supporting the "create savings plan" flow launched with a suggested
    /// name (e.g. from another screen) via <c>ICardInitializable</c>.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_New_Prefill_Sets_Name()
    {
        var (vm, apiMock) = CreateVm();
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());

        (vm as FinanceManager.Web.ViewModels.Common.ICardInitializable)?.SetInitValue("PrefillName");
        (vm as FinanceManager.Web.ViewModels.Common.ICardInitializable)?.SetBackNavigation(null);
        await vm.InitializeAsync(Guid.Empty);

        Assert.False(vm.IsEdit);
        Assert.Equal("PrefillName", vm.Model.Name);
    }

    /// <summary>
    /// Verifies that saving an edited existing plan sends the updated name through the update request and
    /// reports success with no error message set.
    /// </summary>
    [Fact]
    public async Task SaveAsync_Edit_Success()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var dto = new SavingsPlanDto(id, "Plan A", SavingsPlanType.OneTime, 100m, DateTime.UtcNow.Date.AddMonths(6), null, true, DateTime.UtcNow, null, null, null);
        apiMock.Setup(a => a.SavingsPlans_GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());
        await vm.InitializeAsync(id);

        vm.Model.Name = "Updated";
        apiMock.Setup(a => a.SavingsPlans_UpdateAsync(id, It.Is<SavingsPlanCreateRequest>(r => r.Name == "Updated"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsPlanDto(id, "Updated", SavingsPlanType.OneTime, 100m, DateTime.UtcNow.Date.AddMonths(6), null, true, DateTime.UtcNow, null, null, null));

        var ok = await vm.SaveAsync(TestContext.Current.CancellationToken);
        Assert.True(ok);
        Assert.Null(vm.Error);
    }

    /// <summary>
    /// Verifies that saving a new plan (created via <see cref="Guid.Empty"/> initialization) succeeds and
    /// creates it through the create API rather than the update path.
    /// </summary>
    [Fact]
    public async Task SaveAsync_New_Success()
    {
        var (vm, apiMock) = CreateVm();
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());
        await vm.InitializeAsync(Guid.Empty);

        vm.Model.Name = "Created";
        var createdId = Guid.NewGuid();
        apiMock.Setup(a => a.SavingsPlans_CreateAsync(It.IsAny<SavingsPlanCreateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavingsPlanDto(createdId, "Created", SavingsPlanType.OneTime, null, null, null, true, DateTime.UtcNow, null, null, null));

        var ok = await vm.SaveAsync(TestContext.Current.CancellationToken);
        Assert.True(ok);
    }

    /// <summary>
    /// Verifies that a failed delete (API returns <see langword="false"/>) reports failure to the caller
    /// and populates <c>LastError</c> so the UI can inform the user the plan was not removed.
    /// </summary>
    [Fact]
    public async Task Delete_Sets_Error_On_Fail()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var dto = new SavingsPlanDto(id, "Plan A", SavingsPlanType.OneTime, 100m, DateTime.UtcNow.Date.AddMonths(6), null, true, DateTime.UtcNow, null, null, null);
        apiMock.Setup(a => a.SavingsPlans_GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());
        await vm.InitializeAsync(id);

        apiMock.Setup(a => a.SavingsPlans_DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var ok = await vm.DeleteAsync();
        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(vm.LastError));
    }

    /// <summary>
    /// Verifies that loading a plan with analysis data exposes read-only "current amount" and "remaining
    /// amount" currency fields on the card, populated from the analysis result rather than the raw plan DTO.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ShouldExposeCurrentAndRemainingAmountFields()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var dto = CreateSavingsPlanDto(id, currentAmount: 123.45m, remainingAmount: 876.55m);
        apiMock.Setup(a => a.SavingsPlans_GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        apiMock.Setup(a => a.SavingsPlans_AnalyzeAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new SavingsPlanAnalysisDto(id, true, 1000m, dto.TargetDate, 123.45m, 150m, 6));
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());

        await vm.LoadAsync(id);

        var currentAmount = FindField(vm, "Card_Caption_SavingsPlan_CurrentAmount");
        var remainingAmount = FindField(vm, "Card_Caption_SavingsPlan_RemainingAmount");
        Assert.NotNull(currentAmount);
        Assert.Equal(CardFieldKind.Currency, currentAmount.Kind);
        Assert.False(currentAmount.Editable);
        Assert.Equal(123.45m, currentAmount.Amount);
        Assert.NotNull(remainingAmount);
        Assert.Equal(CardFieldKind.Currency, remainingAmount.Kind);
        Assert.False(remainingAmount.Editable);
        Assert.Equal(876.55m, remainingAmount.Amount);
    }

    /// <summary>
    /// Verifies that a fully funded plan (zero remaining amount) hides the "remaining amount" field
    /// entirely rather than showing a misleading "0.00" - the current amount field still appears.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ShouldNotExposeRemainingAmount_WhenRemainingAmountIsZero()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var dto = CreateSavingsPlanDto(id, currentAmount: 1000m, remainingAmount: 0m);
        apiMock.Setup(a => a.SavingsPlans_GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        apiMock.Setup(a => a.SavingsPlans_AnalyzeAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new SavingsPlanAnalysisDto(id, true, 1000m, dto.TargetDate, 1000m, 0m, 6));
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());

        await vm.LoadAsync(id);

        Assert.NotNull(FindField(vm, "Card_Caption_SavingsPlan_CurrentAmount"));
        Assert.Null(FindField(vm, "Card_Caption_SavingsPlan_RemainingAmount"));
    }

    /// <summary>
    /// Verifies the baseline positive case for the "required monthly" figure: a one-time plan with a
    /// future target date and a positive remaining amount exposes it as a read-only currency field taken
    /// directly from the analysis result.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ShouldExposeRequiredMonthly_ForOneTimePlanWithFutureTargetAndRemainingAmount()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var dto = CreateSavingsPlanDto(id, targetDate: DateTime.Today.AddMonths(6), remainingAmount: 750m);
        apiMock.Setup(a => a.SavingsPlans_GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        apiMock.Setup(a => a.SavingsPlans_AnalyzeAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new SavingsPlanAnalysisDto(id, true, 1000m, dto.TargetDate, 250m, 125m, 6));
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());

        await vm.LoadAsync(id);

        var requiredMonthly = FindField(vm, "Card_Caption_SavingsPlan_RequiredMonthly");
        Assert.NotNull(requiredMonthly);
        Assert.Equal(CardFieldKind.Currency, requiredMonthly.Kind);
        Assert.False(requiredMonthly.Editable);
        Assert.Equal(125m, requiredMonthly.Amount);
    }

    /// <summary>
    /// Verifies that "required monthly" is never shown for a recurring plan, even with a future target
    /// and remaining amount, since a fixed monthly contribution is already set by the recurring schedule
    /// rather than derived from a target amortization.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ShouldNotExposeRequiredMonthly_ForRecurringPlan()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var dto = CreateSavingsPlanDto(id, SavingsPlanType.Recurring, targetDate: DateTime.Today.AddMonths(6), remainingAmount: 750m);
        apiMock.Setup(a => a.SavingsPlans_GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        apiMock.Setup(a => a.SavingsPlans_AnalyzeAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new SavingsPlanAnalysisDto(id, true, 1000m, dto.TargetDate, 250m, 125m, 6));
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());

        await vm.LoadAsync(id);

        Assert.Null(FindField(vm, "Card_Caption_SavingsPlan_RequiredMonthly"));
    }

    /// <summary>
    /// Verifies that "required monthly" is hidden once the remaining amount is zero, even for an otherwise
    /// eligible one-time plan with a future target - there is nothing left to save toward.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ShouldNotExposeRequiredMonthly_WhenRemainingAmountIsZero()
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var dto = CreateSavingsPlanDto(id, targetDate: DateTime.Today.AddMonths(6), remainingAmount: 0m);
        apiMock.Setup(a => a.SavingsPlans_GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        apiMock.Setup(a => a.SavingsPlans_AnalyzeAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new SavingsPlanAnalysisDto(id, true, 1000m, dto.TargetDate, 1000m, 125m, 6));
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());

        await vm.LoadAsync(id);

        Assert.Null(FindField(vm, "Card_Caption_SavingsPlan_RequiredMonthly"));
    }

    /// <summary>
    /// Verifies that "required monthly" is hidden once the target date is today or already in the past
    /// (0 or -1 days from today), since a monthly amortization toward a due or overdue target is no longer
    /// a meaningful figure to show.
    /// </summary>
    /// <param name="daysFromToday">Offset from today used as the target date: 0 (today) or -1 (yesterday).</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task LoadAsync_ShouldNotExposeRequiredMonthly_WhenTargetDateIsTodayOrPast(int daysFromToday)
    {
        var (vm, apiMock) = CreateVm();
        var id = Guid.NewGuid();
        var dto = CreateSavingsPlanDto(id, targetDate: DateTime.Today.AddDays(daysFromToday), remainingAmount: 750m);
        apiMock.Setup(a => a.SavingsPlans_GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        apiMock.Setup(a => a.SavingsPlans_AnalyzeAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new SavingsPlanAnalysisDto(id, true, 1000m, dto.TargetDate, 250m, 125m, 6));
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SavingsPlanCategoryDto>());

        await vm.LoadAsync(id);

        Assert.Null(FindField(vm, "Card_Caption_SavingsPlan_RequiredMonthly"));
    }

    /// <summary>
    /// Smoke test verifying that <c>GetRibbonRegisters</c> returns a non-null result. Despite its name,
    /// this does not currently assert that Save is actually disabled for a short name.
    /// </summary>
    [Fact]
    public void Ribbon_Disables_Save_If_Name_Short()
    {
        var (vm, _) = CreateVm();
        var loc = new PassthroughLocalizer<SavingsPlanEditViewModelTests>();
        var regs = vm.GetRibbonRegisters(loc);
        Assert.NotNull(regs);
    }
}

using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Web.ViewModels.Budget;
using Moq;
using BudgetReportDateBasis = FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis;

namespace FinanceManager.Tests.ViewModels;

public sealed class MonthlyBudgetKpiViewModelTests
{
    [Fact]
    public async Task LoadAsync_SetsErrorMessage_OnApiFailure()
    {
        var api = new Mock<IApiClient>();
        api.SetupGet(x => x.LastError).Returns("HTTP 500");
        api.Setup(x => x.Budgets_GetMonthlyKpiAsync(null, FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis.BookingDate, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Response status code does not indicate success: 500"));

        var vm = new MonthlyBudgetKpiViewModel();

        await vm.LoadAsync(api.Object, CancellationToken.None);

        Assert.False(vm.DataLoaded);
        Assert.Equal("HTTP 500", vm.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_Rethrows_UnexpectedExceptions()
    {
        var api = new Mock<IApiClient>();
        api.Setup(x => x.Budgets_GetMonthlyKpiAsync(null, FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis.BookingDate, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unexpected"));

        var vm = new MonthlyBudgetKpiViewModel();

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.LoadAsync(api.Object, CancellationToken.None));
    }

    [Fact]
    public async Task LoadAsync_TreatsNullResponseAsLoadedDefaults()
    {
        var now = new DateTimeOffset(2026, 8, 23, 10, 15, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var api = new Mock<IApiClient>();
        api.Setup(x => x.Budgets_GetMonthlyKpiAsync(null, BudgetReportDateBasis.BookingDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MonthlyBudgetKpiDto)null!);

        var vm = new MonthlyBudgetKpiViewModel
        {
            PlannedIncome = 123m,
            ActualIncome = 456m
        };

        await vm.LoadAsync(api.Object, timeProvider, CancellationToken.None);

        Assert.True(vm.DataLoaded);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal(0m, vm.PlannedIncome);
        Assert.Equal(0m, vm.ActualIncome);
        Assert.Equal(now.UtcDateTime, vm.LoadedAtUtc);
        Assert.Equal(new DateOnly(now.LocalDateTime.Year, now.LocalDateTime.Month, 1), vm.LoadedMonth);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

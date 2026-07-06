using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Budget;
using Moq;

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
}

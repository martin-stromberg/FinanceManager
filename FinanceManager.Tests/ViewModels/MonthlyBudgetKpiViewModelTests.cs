using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Web.ViewModels.Budget;
using Moq;
using BudgetReportDateBasis = FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <see cref="MonthlyBudgetKpiViewModel"/>'s loading behavior, in particular how it
/// distinguishes recoverable API failures (surfaced via <c>ErrorMessage</c>) from unexpected
/// exceptions (which must propagate), and how it normalizes a null API response into
/// well-defined "loaded but empty" defaults instead of leaving stale values in place.
/// </summary>
public sealed class MonthlyBudgetKpiViewModelTests
{
    /// <summary>
    /// Verifies that an <see cref="HttpRequestException"/> from the API call is caught and
    /// surfaced as <c>ErrorMessage</c> (taken from <c>IApiClient.LastError</c>) with
    /// <c>DataLoaded</c> left <see langword="false"/>, so the KPI tile can show a failure state
    /// instead of throwing out of the load pipeline.
    /// </summary>
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

    /// <summary>
    /// Verifies that an exception type other than <see cref="HttpRequestException"/> (e.g. a coding-error
    /// signal like <see cref="InvalidOperationException"/>) is not swallowed into <c>ErrorMessage</c> but
    /// propagates out of <c>LoadAsync</c>, so genuine bugs are not silently hidden behind the KPI tile's
    /// "failed to load" state.
    /// </summary>
    [Fact]
    public async Task LoadAsync_Rethrows_UnexpectedExceptions()
    {
        var api = new Mock<IApiClient>();
        api.Setup(x => x.Budgets_GetMonthlyKpiAsync(null, FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis.BookingDate, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unexpected"));

        var vm = new MonthlyBudgetKpiViewModel();

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.LoadAsync(api.Object, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that a <see langword="null"/> API response (e.g. no budget data exists yet for the period)
    /// is normalized into a successfully "loaded" state with zeroed income figures and a load timestamp/month
    /// derived from the injected <see cref="TimeProvider"/>, rather than leaving previously bound stale values
    /// on the view model or treating the absence of data as an error.
    /// </summary>
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

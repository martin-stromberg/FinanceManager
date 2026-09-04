using Bunit;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Web;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.Services;
using FinanceManager.Web.ViewModels.Budget;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using System.Threading;
using BudgetReportDateBasis = FinanceManager.Shared.Dtos.Budget.BudgetReportDateBasis;

namespace FinanceManager.Tests.Components;

/// <summary>
/// Verifies the loading, rendering, error-handling and auto-refresh behavior of the
/// <see cref="MonthlyBudgetKpi"/> dashboard tile: that it shows a skeleton while its data is in flight, renders
/// the fetched values once loaded, surfaces HTTP and unexpected errors without leaving stale loading UI, avoids
/// issuing duplicate requests on re-render, and periodically re-fetches (recovering from errors and reflecting
/// month changes) based on an injected <see cref="TimeProvider"/> rather than wall-clock time.
/// </summary>
public sealed class MonthlyBudgetKpiTests : BunitContext
{
    /// <summary>
    /// Registers the DI services (logging, localization, string localizer, system time provider) that
    /// <see cref="MonthlyBudgetKpi"/> needs in order to render inside the bUnit test context.
    /// </summary>
    public MonthlyBudgetKpiTests()
    {
        Services.AddLogging();
        Services.AddLocalization(options => options.ResourcesPath = "Resources");
        Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());
        Services.AddSingleton(TimeProvider.System);
    }

    /// <summary>
    /// Verifies that while the KPI's API call is still pending, the component shows the loading overlay and
    /// spinner and does not yet render the fill bar or result text - i.e. no stale or default values flash
    /// on screen before real data arrives.
    /// </summary>
    [Fact]
    public void SlowRequest_RendersSkeletonBeforeValues()
    {
        var pendingKpi = new TaskCompletionSource<MonthlyBudgetKpiDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var apiMock = CreateApiReturning(pendingKpi.Task);
        Services.AddSingleton(apiMock.Object);

        var cut = Render<MonthlyBudgetKpi>(parameters => parameters
            .Add(p => p.ViewModel, new MonthlyBudgetKpiViewModel()));

        apiMock.Verify(a => a.Budgets_GetMonthlyKpiAsync(
            null,
            BudgetReportDateBasis.BookingDate,
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotEmpty(cut.FindAll(".budget-loading-overlay"));
        Assert.NotEmpty(cut.FindAll(".budget-loading-spinner"));
        Assert.Empty(cut.FindAll(".budget-fill"));
        Assert.Empty(cut.FindAll(".budget-result"));
    }

    /// <summary>
    /// Verifies that once the pending KPI request completes, the loading overlay is removed, the budget fill
    /// bar appears, and the result text reflects the fetched value - confirming the component transitions
    /// cleanly from loading state to populated state.
    /// </summary>
    [Fact]
    public void CompletedRequest_RendersValuesAndRemovesSkeleton()
    {
        var pendingKpi = new TaskCompletionSource<MonthlyBudgetKpiDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var apiMock = CreateApiReturning(pendingKpi.Task);
        Services.AddSingleton(apiMock.Object);

        var cut = Render<MonthlyBudgetKpi>(parameters => parameters
            .Add(p => p.ViewModel, new MonthlyBudgetKpiViewModel()));

        pendingKpi.SetResult(CreateKpiDto());

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".budget-loading-overlay"));
            Assert.NotEmpty(cut.FindAll(".budget-fill"));
            Assert.Contains("900", cut.Find(".budget-result").TextContent);
        });
    }

    /// <summary>
    /// Verifies that calling <c>cut.Render()</c> again on a component bound to the same, unchanged view model
    /// does not trigger a second call to <c>Budgets_GetMonthlyKpiAsync</c>. Guards against the component
    /// re-fetching on every Blazor re-render instead of only when the underlying view model actually needs data.
    /// </summary>
    [Fact]
    public void RepeatedRender_StartsOnlyOneRequestForSameViewModel()
    {
        var pendingKpi = new TaskCompletionSource<MonthlyBudgetKpiDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var apiMock = CreateApiReturning(pendingKpi.Task);
        Services.AddSingleton(apiMock.Object);
        var viewModel = new MonthlyBudgetKpiViewModel();

        var cut = Render<MonthlyBudgetKpi>(parameters => parameters
            .Add(p => p.ViewModel, viewModel));

        cut.Render();

        apiMock.Verify(a => a.Budgets_GetMonthlyKpiAsync(
            null,
            BudgetReportDateBasis.BookingDate,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that when the API call fails with an <see cref="HttpRequestException"/> and the client already
    /// carries a last-error message, the component renders the error state and clears the loading overlay
    /// instead of leaving the tile stuck showing a spinner indefinitely.
    /// </summary>
    [Fact]
    public void HttpError_RendersExistingErrorState()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.SetupGet(a => a.LastError).Returns("HTTP 500");
        apiMock.Setup(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("failed"));
        Services.AddSingleton(apiMock.Object);

        var cut = Render<MonthlyBudgetKpi>(parameters => parameters
            .Add(p => p.ViewModel, new MonthlyBudgetKpiViewModel()));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".error")));
        Assert.Empty(cut.FindAll(".budget-loading-overlay"));
    }

    /// <summary>
    /// Verifies that an unexpected exception type (<see cref="InvalidOperationException"/>, not just the
    /// HTTP-specific failure path) thrown by the API call is still caught and surfaced as the error state,
    /// with no loading overlay or partial result left rendered - i.e. error handling is not narrowly typed
    /// to HTTP failures only.
    /// </summary>
    [Fact]
    public void UnexpectedError_RendersErrorState()
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unexpected"));
        Services.AddSingleton(apiMock.Object);

        var cut = Render<MonthlyBudgetKpi>(parameters => parameters
            .Add(p => p.ViewModel, new MonthlyBudgetKpiViewModel()));

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll(".error"));
            Assert.Empty(cut.FindAll(".budget-loading-overlay"));
            Assert.Empty(cut.FindAll(".budget-result"));
        });
    }

    /// <summary>
    /// Verifies that after an initial request fails, the component automatically retries once its refresh
    /// interval elapses (simulated via the injected <see cref="ManualTimeProvider"/> rather than a real timer),
    /// and that a subsequent successful response clears the error state and renders the recovered value -
    /// confirming the tile self-heals from a transient failure without requiring a page reload.
    /// </summary>
    [Fact]
    public void ErrorRefreshIntervalElapsed_StartsFreshRequestAndRendersRecoveredValues()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));
        Services.AddSingleton<TimeProvider>(timeProvider);
        var apiMock = new Mock<IApiClient>();
        var callCount = 0;
        apiMock.Setup(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()))
            .Returns(() => Interlocked.Increment(ref callCount) == 1
                ? Task.FromException<MonthlyBudgetKpiDto>(new HttpRequestException("temporary"))
                : Task.FromResult(CreateKpiDto(actualIncome: 1_500m)));
        Services.AddSingleton(apiMock.Object);

        var cut = Render<MonthlyBudgetKpi>(parameters => parameters
            .Add(p => p.ViewModel, new MonthlyBudgetKpiViewModel()));

        cut.WaitForAssertion(() =>
        {
            apiMock.Verify(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotEmpty(cut.FindAll(".error"));
        });

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        cut.WaitForAssertion(() =>
        {
            apiMock.Verify(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.Empty(cut.FindAll(".error"));
            Assert.Contains("1200", cut.Find(".budget-result").TextContent);
        });
    }

    /// <summary>
    /// Verifies that when the component is mounted with a view model that already finished loading data
    /// (<c>LoadAsync</c> was awaited before <c>Render</c>), it still starts its periodic refresh observer -
    /// so a KPI pre-populated by a parent component (e.g. for instant first paint) keeps refreshing on its
    /// normal schedule rather than only refreshing when it performs the initial load itself.
    /// </summary>
    [Fact]
    public async Task PreloadedViewModel_StartsRefreshObserverAfterMount()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));
        Services.AddSingleton<TimeProvider>(timeProvider);
        var apiMock = CreateApiReturningSequence(CreateKpiDto(actualIncome: 1_500m));
        Services.AddSingleton(apiMock.Object);
        var viewModel = new MonthlyBudgetKpiViewModel();
        await viewModel.LoadAsync(CreateApiReturning(Task.FromResult(CreateKpiDto())).Object, timeProvider, CancellationToken.None);

        var cut = Render<MonthlyBudgetKpi>(parameters => parameters
            .Add(p => p.ViewModel, viewModel));

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        cut.WaitForAssertion(() =>
        {
            apiMock.Verify(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.Contains("1200", cut.Find(".budget-result").TextContent);
        });
    }

    /// <summary>
    /// Verifies that when the component is mounted with a view model that was already marked as failed
    /// (<c>MarkLoadFailed</c> called before <c>Render</c>), it still starts its retry observer and recovers
    /// once the refresh interval elapses - mirroring <see cref="PreloadedViewModel_StartsRefreshObserverAfterMount"/>
    /// but for the pre-failed case, so a tile that failed to load before being mounted is not stuck forever.
    /// </summary>
    [Fact]
    public void PreFailedViewModel_StartsRetryObserverAfterMount()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));
        Services.AddSingleton<TimeProvider>(timeProvider);
        var apiMock = CreateApiReturningSequence(CreateKpiDto(actualIncome: 1_500m));
        Services.AddSingleton(apiMock.Object);
        var viewModel = new MonthlyBudgetKpiViewModel();
        viewModel.MarkLoadFailed("temporary");

        var cut = Render<MonthlyBudgetKpi>(parameters => parameters
            .Add(p => p.ViewModel, viewModel));

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        cut.WaitForAssertion(() =>
        {
            apiMock.Verify(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.Empty(cut.FindAll(".error"));
            Assert.Contains("1200", cut.Find(".budget-result").TextContent);
        });
    }

    /// <summary>
    /// Verifies that the component decides whether the KPI covers the "current" month by asking the injected
    /// <see cref="TimeProvider"/> (here set a month ahead of the real system clock) rather than reading
    /// <see cref="DateTime.UtcNow"/> directly, so the "current month" styling (<c>budget-text-current</c> CSS
    /// class) stays testable and correct independent of when the test actually runs.
    /// </summary>
    [Fact]
    public void CurrentMonthDisplay_UsesInjectedTimeProvider()
    {
        var providerMonthDifferentFromSystemMonth = new DateTimeOffset(DateTime.UtcNow.AddMonths(1).Year, DateTime.UtcNow.AddMonths(1).Month, 1, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(providerMonthDifferentFromSystemMonth);
        Services.AddSingleton<TimeProvider>(timeProvider);
        var apiMock = CreateApiReturning(Task.FromResult(CreateKpiDto()));
        Services.AddSingleton(apiMock.Object);

        var cut = Render<MonthlyBudgetKpi>(parameters => parameters
            .Add(p => p.ViewModel, new MonthlyBudgetKpiViewModel()));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("budget-text-current", cut.Find(".budget-result").ClassList);
        });
    }

    /// <summary>
    /// Verifies that explicitly invalidating the view model (<c>viewModel.Invalidate()</c>) followed by a
    /// re-render causes the component to issue a brand-new API request rather than reusing the previously
    /// loaded data - the mechanism callers rely on to force a KPI to reload on demand (e.g. after a related
    /// edit elsewhere in the app), independent of the periodic auto-refresh interval.
    /// </summary>
    [Fact]
    public void InvalidatedViewModel_StartsFreshRequest()
    {
        var firstKpi = new TaskCompletionSource<MonthlyBudgetKpiDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondKpi = new TaskCompletionSource<MonthlyBudgetKpiDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var apiMock = new Mock<IApiClient>();
        var callCount = 0;
        apiMock.Setup(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()))
            .Returns(() => Interlocked.Increment(ref callCount) == 1 ? firstKpi.Task : secondKpi.Task);
        Services.AddSingleton(apiMock.Object);
        var viewModel = new MonthlyBudgetKpiViewModel();

        var cut = Render<MonthlyBudgetKpi>(parameters => parameters
            .Add(p => p.ViewModel, viewModel));

        firstKpi.SetResult(CreateKpiDto());
        cut.WaitForAssertion(() => Assert.True(viewModel.DataLoaded));

        viewModel.Invalidate();
        cut.Render();

        cut.WaitForAssertion(() =>
        {
            apiMock.Verify(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()), Times.Exactly(2));
        });
    }

    /// <summary>
    /// Verifies the "happy path" of periodic auto-refresh: once the refresh interval elapses (simulated via
    /// <see cref="ManualTimeProvider.Advance"/>), the component issues a second request and re-renders with
    /// the newly returned values, replacing the stale result already shown from the first load.
    /// </summary>
    [Fact]
    public void RefreshIntervalElapsed_StartsFreshRequestAndRendersUpdatedValues()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));
        Services.AddSingleton<TimeProvider>(timeProvider);
        var apiMock = CreateApiReturningSequence(CreateKpiDto(actualIncome: 1_200m), CreateKpiDto(actualIncome: 1_500m));
        Services.AddSingleton(apiMock.Object);

        var cut = Render<MonthlyBudgetKpi>(parameters => parameters
            .Add(p => p.ViewModel, new MonthlyBudgetKpiViewModel()));

        cut.WaitForAssertion(() =>
        {
            apiMock.Verify(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.Contains("900", cut.Find(".budget-result").TextContent);
        });

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        cut.WaitForAssertion(() =>
        {
            apiMock.Verify(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.Contains("1200", cut.Find(".budget-result").TextContent);
        });
    }

    /// <summary>
    /// Verifies auto-refresh behavior specifically across a month boundary: starting just before local
    /// midnight on the last day of the month, advancing time past the refresh interval crosses into the next
    /// month, and the component still issues a fresh request and renders the updated values - guarding
    /// against the refresh logic being tied to a fixed "current month" it computed once at mount time.
    /// </summary>
    [Fact]
    public void MonthChanged_StartsFreshRequestAndRendersUpdatedValues()
    {
        var localTimeZone = TimeZoneInfo.Local;
        var startLocal = new DateTime(2026, 8, 31, 23, 58, 0, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, localTimeZone);
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(startUtc, TimeSpan.Zero));
        Services.AddSingleton<TimeProvider>(timeProvider);
        var apiMock = CreateApiReturningSequence(CreateKpiDto(actualIncome: 1_200m), CreateKpiDto(actualIncome: 1_700m));
        Services.AddSingleton(apiMock.Object);

        var cut = Render<MonthlyBudgetKpi>(parameters => parameters
            .Add(p => p.ViewModel, new MonthlyBudgetKpiViewModel()));

        cut.WaitForAssertion(() =>
        {
            apiMock.Verify(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.Contains("900", cut.Find(".budget-result").TextContent);
        });

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        cut.WaitForAssertion(() =>
        {
            apiMock.Verify(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.Contains("1400", cut.Find(".budget-result").TextContent);
        });
    }

    private static Mock<IApiClient> CreateApiReturning(Task<MonthlyBudgetKpiDto> task)
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()))
            .Returns(task);
        return apiMock;
    }

    private static Mock<IApiClient> CreateApiReturningSequence(params MonthlyBudgetKpiDto[] results)
    {
        var apiMock = new Mock<IApiClient>();
        var callCount = 0;
        apiMock.Setup(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(results[Math.Min(Interlocked.Increment(ref callCount), results.Length) - 1]));
        return apiMock;
    }

    private static MonthlyBudgetKpiDto CreateKpiDto(decimal actualIncome = 1_200m) => new()
    {
        PlannedIncome = 1_000m,
        PlannedExpenseAbs = 400m,
        ActualIncome = actualIncome,
        ActualExpenseAbs = 300m,
        PlannedResult = 600m,
        ExpectedIncome = actualIncome,
        ExpectedExpenseAbs = 400m,
        UnbudgetedIncome = Math.Max(actualIncome - 1_000m, 0m),
        UnbudgetedExpenseAbs = 0m
    };

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private readonly object _gate = new();
        private readonly List<ManualTimer> _timers = [];
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            lock (_gate)
            {
                return _utcNow;
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            return timer;
        }

        public void Advance(TimeSpan duration)
        {
            List<ManualTimer> dueTimers;
            lock (_gate)
            {
                _utcNow = _utcNow.Add(duration);
                dueTimers = _timers.Where(timer => timer.IsDue(_utcNow)).ToList();
            }

            foreach (var timer in dueTimers)
            {
                timer.Fire();
            }
        }

        private void Register(ManualTimer timer)
        {
            lock (_gate)
            {
                if (!_timers.Contains(timer))
                {
                    _timers.Add(timer);
                }
            }
        }

        private void Unregister(ManualTimer timer)
        {
            lock (_gate)
            {
                _timers.Remove(timer);
            }
        }

        private sealed class ManualTimer(
            ManualTimeProvider owner,
            TimerCallback callback,
            object? state) : ITimer
        {
            private TimeSpan _period;
            private DateTimeOffset? _dueAtUtc;
            private bool _disposed;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (_disposed)
                {
                    return false;
                }

                _period = period;
                if (dueTime == Timeout.InfiniteTimeSpan)
                {
                    _dueAtUtc = null;
                    owner.Unregister(this);
                    return true;
                }

                _dueAtUtc = owner.GetUtcNow().Add(dueTime);
                owner.Register(this);
                return true;
            }

            public bool IsDue(DateTimeOffset utcNow) => !_disposed && _dueAtUtc.HasValue && _dueAtUtc.Value <= utcNow;

            public void Fire()
            {
                if (_disposed)
                {
                    return;
                }

                if (_period == Timeout.InfiniteTimeSpan)
                {
                    _dueAtUtc = null;
                    owner.Unregister(this);
                }
                else
                {
                    _dueAtUtc = owner.GetUtcNow().Add(_period);
                }

                callback(state);
            }

            public void Dispose()
            {
                _disposed = true;
                owner.Unregister(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}

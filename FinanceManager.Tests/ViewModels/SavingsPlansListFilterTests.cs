using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.SavingsPlans;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers the search and date-range filtering of <see cref="SavingsPlansListViewModel"/>:
/// substring search on name/contract number/category name, inclusive target-date filtering
/// for one-time plans and due-date enumeration for recurring plans.
/// </summary>
public sealed class SavingsPlansListFilterTests
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
        public LocalizedString this[string name] => new LocalizedString(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new LocalizedString(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) { yield break; }
    }

    private static (SavingsPlansListViewModel vm, Mock<IApiClient> apiMock) CreateVm(IReadOnlyList<SavingsPlanDto> plans, IReadOnlyList<SavingsPlanCategoryDto>? categories = null)
    {
        var apiMock = new Mock<IApiClient>();
        apiMock.Setup(a => a.SavingsPlans_ListAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans);
        apiMock.Setup(a => a.SavingsPlanCategories_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories ?? new List<SavingsPlanCategoryDto>());
        apiMock.Setup(a => a.SavingsPlans_AnalyzeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new SavingsPlanAnalysisDto(id, false, null, null, 0m, 0m, 0));

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton(apiMock.Object);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(TestLocalizer<>));
        var sp = services.BuildServiceProvider();
        return (new SavingsPlansListViewModel(sp), apiMock);
    }

    private static SavingsPlanDto Plan(string name, SavingsPlanType type, DateTime? targetDate, SavingsPlanInterval? interval = null, string? contractNumber = null, Guid? categoryId = null, DateTime? createdUtc = null, DateTime? archivedUtc = null)
        => new SavingsPlanDto(Guid.NewGuid(), name, type, null, targetDate, interval, true, createdUtc ?? new DateTime(2020, 1, 1), archivedUtc, categoryId, contractNumber, null);

    /// <summary>
    /// A search term matching the plan name filters the list down to that plan (case-insensitive).
    /// </summary>
    [Fact]
    public async Task Search_ByName_Filters()
    {
        var (vm, _) = CreateVm(new List<SavingsPlanDto>
        {
            Plan("Altersvorsorge", SavingsPlanType.OneTime, null),
            Plan("Urlaub", SavingsPlanType.OneTime, null)
        });

        vm.SetSearch("alter");
        await vm.LoadAsync();

        Assert.Single(vm.Items);
        Assert.Equal("Altersvorsorge", vm.Items[0].Name);
    }

    /// <summary>
    /// The search also applies to the contract number and the resolved category name.
    /// </summary>
    [Fact]
    public async Task Search_ByContractNumberAndCategory_Filters()
    {
        var catId = Guid.NewGuid();
        var plans = new List<SavingsPlanDto>
        {
            Plan("P1", SavingsPlanType.OneTime, null, contractNumber: "CN-4711"),
            Plan("P2", SavingsPlanType.OneTime, null, categoryId: catId),
            Plan("P3", SavingsPlanType.OneTime, null)
        };
        var categories = new List<SavingsPlanCategoryDto>
        {
            new SavingsPlanCategoryDto { Id = catId, Name = "Rücklagen" }
        };

        var (vm, _) = CreateVm(plans, categories);

        vm.SetSearch("cn-47");
        await vm.LoadAsync();
        Assert.Single(vm.Items);
        Assert.Equal("P1", vm.Items[0].Name);

        vm.SetSearch("rücklagen");
        await vm.LoadAsync();
        Assert.Single(vm.Items);
        Assert.Equal("P2", vm.Items[0].Name);

        vm.SetSearch(string.Empty);
        await vm.LoadAsync();
        Assert.Equal(3, vm.Items.Count);
    }

    /// <summary>
    /// A one-time plan is a hit exactly when its target date lies inside the inclusive range.
    /// </summary>
    [Fact]
    public async Task Range_OneTime_TargetDateInRange_Inclusive()
    {
        var (vm, _) = CreateVm(new List<SavingsPlanDto>
        {
            Plan("in", SavingsPlanType.OneTime, new DateTime(2025, 6, 15)),
            Plan("edgeFrom", SavingsPlanType.OneTime, new DateTime(2025, 6, 1)),
            Plan("edgeTo", SavingsPlanType.OneTime, new DateTime(2025, 6, 30)),
            Plan("out", SavingsPlanType.OneTime, new DateTime(2025, 7, 1))
        });

        vm.SetRange(new DateTime(2025, 6, 1), new DateTime(2025, 6, 30));
        await vm.LoadAsync();

        Assert.Equal(3, vm.Items.Count);
        Assert.DoesNotContain(vm.Items, i => i.Name == "out");
    }

    /// <summary>
    /// With only one bound set the range is open-ended; swapped bounds are normalized.
    /// </summary>
    [Fact]
    public async Task Range_SingleBound_AndSwappedBounds()
    {
        var (vm, _) = CreateVm(new List<SavingsPlanDto>
        {
            Plan("early", SavingsPlanType.OneTime, new DateTime(2025, 1, 15)),
            Plan("late", SavingsPlanType.OneTime, new DateTime(2025, 12, 15))
        });

        vm.SetRange(new DateTime(2025, 6, 1), null);
        await vm.LoadAsync();
        Assert.Single(vm.Items);
        Assert.Equal("late", vm.Items[0].Name);

        vm.SetRange(null, new DateTime(2025, 6, 1));
        await vm.LoadAsync();
        Assert.Single(vm.Items);
        Assert.Equal("early", vm.Items[0].Name);

        vm.SetRange(new DateTime(2025, 12, 31), new DateTime(2025, 1, 1));
        await vm.LoadAsync();
        Assert.Equal(2, vm.Items.Count);
    }

    /// <summary>
    /// Plans without a target date (e.g. Type = Open) are hidden while a range filter is set.
    /// </summary>
    [Fact]
    public async Task Range_OpenPlanWithoutTargetDate_Hidden()
    {
        var (vm, _) = CreateVm(new List<SavingsPlanDto>
        {
            Plan("open", SavingsPlanType.Open, null),
            Plan("dated", SavingsPlanType.OneTime, new DateTime(2025, 6, 15))
        });

        vm.SetRange(new DateTime(2025, 6, 1), new DateTime(2025, 6, 30));
        await vm.LoadAsync();

        Assert.Single(vm.Items);
        Assert.Equal("dated", vm.Items[0].Name);
    }

    /// <summary>
    /// A recurring plan matches when a past due date falls into the range even though the
    /// displayed next target date lies after the range.
    /// </summary>
    [Fact]
    public async Task Range_Recurring_PastDueDateInRange()
    {
        var monthly = Plan("monthly", SavingsPlanType.Recurring, new DateTime(2026, 6, 15), SavingsPlanInterval.Monthly, createdUtc: new DateTime(2020, 1, 1));
        var (vm, _) = CreateVm(new List<SavingsPlanDto> { monthly });

        vm.SetRange(new DateTime(2025, 3, 1), new DateTime(2025, 3, 31));
        await vm.LoadAsync();

        Assert.Single(vm.Items);
    }

    /// <summary>
    /// A recurring plan matches when a future due date falls into the range even though the
    /// next target date lies before the range.
    /// </summary>
    [Fact]
    public async Task Range_Recurring_FutureDueDateInRange()
    {
        var monthly = Plan("monthly", SavingsPlanType.Recurring, new DateTime(2026, 6, 15), SavingsPlanInterval.Monthly);
        var (vm, _) = CreateVm(new List<SavingsPlanDto> { monthly });

        vm.SetRange(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));
        await vm.LoadAsync();

        Assert.Single(vm.Items);
    }

    /// <summary>
    /// A recurring plan does not match when no due date falls into the range.
    /// </summary>
    [Fact]
    public async Task Range_Recurring_NoDueDateInRange()
    {
        // quarterly dues: Mar 15 / Jun 15 / Sep 15 / Dec 15 — April never has a due
        var quarterly = Plan("quarterly", SavingsPlanType.Recurring, new DateTime(2026, 6, 15), SavingsPlanInterval.Quarterly, createdUtc: new DateTime(2020, 1, 1));
        var (vm, _) = CreateVm(new List<SavingsPlanDto> { quarterly });

        vm.SetRange(new DateTime(2026, 4, 1), new DateTime(2026, 4, 30));
        await vm.LoadAsync();

        Assert.Empty(vm.Items);
    }

    /// <summary>
    /// With only a lower bound a recurring plan always matches — future due dates are unbounded.
    /// </summary>
    [Fact]
    public async Task Range_Recurring_OnlyFromBound_Matches()
    {
        var monthly = Plan("monthly", SavingsPlanType.Recurring, new DateTime(2026, 6, 15), SavingsPlanInterval.Monthly);
        var (vm, _) = CreateVm(new List<SavingsPlanDto> { monthly });

        vm.SetRange(new DateTime(2099, 1, 1), null);
        await vm.LoadAsync();

        Assert.Single(vm.Items);
    }

    /// <summary>
    /// With only an upper bound a recurring plan matches when any due date exists before it
    /// (lower bound is the plan creation date).
    /// </summary>
    [Fact]
    public async Task Range_Recurring_OnlyToBound()
    {
        var monthly = Plan("monthly", SavingsPlanType.Recurring, new DateTime(2026, 6, 15), SavingsPlanInterval.Monthly, createdUtc: new DateTime(2025, 5, 10));
        var (vm, _) = CreateVm(new List<SavingsPlanDto> { monthly });

        vm.SetRange(null, new DateTime(2025, 6, 30));
        await vm.LoadAsync();
        Assert.Single(vm.Items);

        vm.SetRange(null, new DateTime(2025, 5, 5));
        await vm.LoadAsync();
        Assert.Empty(vm.Items);
    }

    /// <summary>
    /// Recurring due dates on the exact range bounds count as hits (inclusive bounds).
    /// </summary>
    [Fact]
    public async Task Range_Recurring_DueOnBounds_Inclusive()
    {
        var monthly = Plan("monthly", SavingsPlanType.Recurring, new DateTime(2026, 6, 15), SavingsPlanInterval.Monthly, createdUtc: new DateTime(2020, 1, 1));
        var (vm, _) = CreateVm(new List<SavingsPlanDto> { monthly });

        vm.SetRange(new DateTime(2025, 8, 15), new DateTime(2025, 8, 15));
        await vm.LoadAsync();
        Assert.Single(vm.Items);

        vm.SetRange(new DateTime(2025, 8, 16), new DateTime(2025, 9, 14));
        await vm.LoadAsync();
        Assert.Empty(vm.Items);
    }

    /// <summary>
    /// A recurring plan with a target date already passed (auto-advance not yet run) still matches
    /// via dues enumerated forward from that target date.
    /// </summary>
    [Fact]
    public async Task Range_Recurring_TargetDateInPast_ForwardDues()
    {
        var monthly = Plan("stale", SavingsPlanType.Recurring, new DateTime(2025, 1, 15), SavingsPlanInterval.Monthly, createdUtc: new DateTime(2024, 1, 1));
        var (vm, _) = CreateVm(new List<SavingsPlanDto> { monthly });

        vm.SetRange(new DateTime(2025, 3, 1), new DateTime(2025, 3, 31));
        await vm.LoadAsync();
        Assert.Single(vm.Items);
    }

    /// <summary>
    /// A recurring plan without an interval falls back to its single target date.
    /// </summary>
    [Fact]
    public async Task Range_Recurring_WithoutInterval_TreatedAsSingleDate()
    {
        var broken = Plan("broken", SavingsPlanType.Recurring, new DateTime(2025, 6, 15), null);
        var (vm, _) = CreateVm(new List<SavingsPlanDto> { broken });

        vm.SetRange(new DateTime(2025, 6, 1), new DateTime(2025, 6, 30));
        await vm.LoadAsync();
        Assert.Single(vm.Items);

        vm.SetRange(new DateTime(2025, 7, 1), new DateTime(2025, 7, 31));
        await vm.LoadAsync();
        Assert.Empty(vm.Items);
    }

    /// <summary>
    /// An archived recurring plan only matches ranges containing dues up to its archive date —
    /// an open-ended range starting after the archive date does not match.
    /// </summary>
    [Fact]
    public async Task Range_Recurring_Archived_BoundedByArchiveDate()
    {
        var archived = Plan("archived", SavingsPlanType.Recurring, new DateTime(2025, 12, 15), SavingsPlanInterval.Monthly, createdUtc: new DateTime(2025, 1, 1), archivedUtc: new DateTime(2025, 10, 20));
        var (vm, _) = CreateVm(new List<SavingsPlanDto> { archived });

        // dues Oct 15 and earlier exist — range before archive matches
        vm.SetRange(new DateTime(2025, 9, 1), new DateTime(2025, 9, 30));
        await vm.LoadAsync();
        Assert.Single(vm.Items);

        // open-ended range starting after the archive date: no dues anymore
        vm.SetRange(new DateTime(2026, 1, 1), null);
        await vm.LoadAsync();
        Assert.Empty(vm.Items);

        // bounded range fully after archive: no match
        vm.SetRange(new DateTime(2025, 11, 1), new DateTime(2025, 11, 30));
        await vm.LoadAsync();
        Assert.Empty(vm.Items);
    }

    /// <summary>
    /// Documents the accepted reconstruction semantics: past dues are enumerated as
    /// <c>TargetDate − n·Interval</c> with month-end propagation. A plan anchored on the 30th whose
    /// stored next due is Mar 31 (month-end drift) reconstructs past dues on month ends.
    /// </summary>
    [Fact]
    public async Task Range_Recurring_MonthEndDrift_ReconstructedSeries()
    {
        var monthly = Plan("drifted", SavingsPlanType.Recurring, new DateTime(2024, 3, 31), SavingsPlanInterval.Monthly, createdUtc: new DateTime(2024, 1, 30));
        var (vm, _) = CreateVm(new List<SavingsPlanDto> { monthly });

        // reconstructed dues: Jan 31, Feb 29, Mar 31 — a range covering the 31st hits, one covering
        // only the 30th does not (the real anchor's dues cannot be recovered from TargetDate alone)
        vm.SetRange(new DateTime(2024, 1, 31), new DateTime(2024, 1, 31));
        await vm.LoadAsync();
        Assert.Single(vm.Items);

        vm.SetRange(new DateTime(2024, 1, 30), new DateTime(2024, 1, 30));
        await vm.LoadAsync();
        Assert.Empty(vm.Items);
    }

    /// <summary>
    /// Due dates before plan creation do not count — a range lying fully before CreatedUtc
    /// yields no hit.
    /// </summary>
    [Fact]
    public async Task Range_Recurring_DueDatesBeforeCreation_Ignored()
    {
        var monthly = Plan("monthly", SavingsPlanType.Recurring, new DateTime(2026, 6, 15), SavingsPlanInterval.Monthly, createdUtc: new DateTime(2025, 5, 10));
        var (vm, _) = CreateVm(new List<SavingsPlanDto> { monthly });

        vm.SetRange(new DateTime(2025, 1, 1), new DateTime(2025, 1, 31));
        await vm.LoadAsync();

        Assert.Empty(vm.Items);
    }

    /// <summary>
    /// Search and date range combine with AND — a plan must satisfy both filters.
    /// </summary>
    [Fact]
    public async Task Search_AndRange_CombineAnd()
    {
        var (vm, _) = CreateVm(new List<SavingsPlanDto>
        {
            Plan("Alpha", SavingsPlanType.OneTime, new DateTime(2025, 6, 15)),
            Plan("Alpha Two", SavingsPlanType.OneTime, new DateTime(2025, 9, 15)),
            Plan("Beta", SavingsPlanType.OneTime, new DateTime(2025, 6, 15))
        });

        vm.SetSearch("alpha");
        vm.SetRange(new DateTime(2025, 6, 1), new DateTime(2025, 6, 30));
        await vm.LoadAsync();

        Assert.Single(vm.Items);
        Assert.Equal("Alpha", vm.Items[0].Name);
    }

    /// <summary>
    /// The analysis call (N+1) only runs for plans surviving the filters.
    /// </summary>
    [Fact]
    public async Task FilteredPlans_SkipAnalysisCalls()
    {
        var plans = new List<SavingsPlanDto>
        {
            Plan("Alpha", SavingsPlanType.OneTime, new DateTime(2025, 6, 15)),
            Plan("Beta", SavingsPlanType.OneTime, new DateTime(2025, 9, 15))
        };
        var (vm, apiMock) = CreateVm(plans);

        vm.SetRange(new DateTime(2025, 6, 1), new DateTime(2025, 6, 30));
        await vm.LoadAsync();

        apiMock.Verify(a => a.SavingsPlans_AnalyzeAsync(plans[0].Id, It.IsAny<CancellationToken>()), Times.Once);
        apiMock.Verify(a => a.SavingsPlans_AnalyzeAsync(plans[1].Id, It.IsAny<CancellationToken>()), Times.Never);
    }
}

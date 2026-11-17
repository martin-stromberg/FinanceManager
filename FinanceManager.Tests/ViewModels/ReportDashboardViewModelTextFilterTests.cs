using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Reports;
using FinanceManager.Web.ViewModels.Reports;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers the free-text filter support of <see cref="ReportDashboardViewModel"/>: the temp-buffer add/remove
/// workflow (trim, empty-ignore, case-insensitive dedupe), the copy semantics between selected and temp lists
/// when opening/applying/clearing the filter dialog, the payload mapping into
/// <see cref="ReportAggregatesFiltersRequest"/>, the filter counters and favorite save/update mapping.
/// </summary>
public sealed class ReportDashboardViewModelTextFilterTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private static (ReportDashboardViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        var sp = services.BuildServiceProvider();
        return (new ReportDashboardViewModel(sp), apiMock);
    }

    private static void SetupAggregates(Mock<IApiClient> apiMock, List<ReportAggregatesQueryRequest> captured)
    {
        apiMock.Setup(a => a.Reports_QueryAggregatesAsync(It.IsAny<ReportAggregatesQueryRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReportAggregatesQueryRequest, CancellationToken>((r, _) => captured.Add(r))
            .ReturnsAsync(new ReportAggregationResult(ReportInterval.Month, Array.Empty<ReportAggregatePointDto>(), false, false, false));
    }

    /// <summary>
    /// Adding a temp text filter trims the input; empty input is ignored and case-insensitive
    /// duplicates are rejected, and a successful add clears the input field.
    /// </summary>
    [Fact]
    public void AddTempTextFilter_ShouldTrim_Dedupe_AndIgnoreEmpty()
    {
        var (vm, _) = CreateVm();
        vm.OpenFilterDialog();

        vm.NewTextFilterInput = "  Miete  ";
        vm.AddTempTextFilter(PostingKind.Bank, vm.NewTextFilterInput);
        Assert.Equal(new[] { "Miete" }, vm.GetTempTextFilters(PostingKind.Bank));
        Assert.Equal(string.Empty, vm.NewTextFilterInput);

        // case-insensitive duplicate is not added
        vm.NewTextFilterInput = "MIETE";
        vm.AddTempTextFilter(PostingKind.Bank, vm.NewTextFilterInput);
        Assert.Equal(new[] { "Miete" }, vm.GetTempTextFilters(PostingKind.Bank));

        // empty input is ignored
        vm.AddTempTextFilter(PostingKind.Bank, "   ");
        Assert.Single(vm.GetTempTextFilters(PostingKind.Bank));
    }

    /// <summary>
    /// Removing a temp text filter works case-insensitively and only affects the given posting kind.
    /// </summary>
    [Fact]
    public void RemoveTempTextFilter_ShouldRemove_OnlyForGivenKind()
    {
        var (vm, _) = CreateVm();
        vm.OpenFilterDialog();
        vm.AddTempTextFilter(PostingKind.Bank, "Miete");
        vm.AddTempTextFilter(PostingKind.Contact, "Miete");

        vm.RemoveTempTextFilter(PostingKind.Bank, "MIETE");

        Assert.Empty(vm.GetTempTextFilters(PostingKind.Bank));
        Assert.Equal(new[] { "Miete" }, vm.GetTempTextFilters(PostingKind.Contact));
    }

    /// <summary>
    /// Applying the filter dialog copies the temp text filters into the selected state and the
    /// resulting aggregation request payload carries the per-kind text filter lists.
    /// </summary>
    [Fact]
    public async Task ApplyTempAndReload_ShouldMapTextFilters_IntoRequestPayload()
    {
        var (vm, apiMock) = CreateVm();
        var captured = new List<ReportAggregatesQueryRequest>();
        SetupAggregates(apiMock, captured);

        await vm.LoadAsync(PostingKind.Bank, 0, 24, false, false, false, false, new[] { PostingKind.Bank }, DateTime.UtcNow, null, ct: TestContext.Current.CancellationToken);

        vm.OpenFilterDialog();
        vm.AddTempTextFilter(PostingKind.Bank, "Miete");
        vm.AddTempTextFilter(PostingKind.Bank, "Tankstelle");
        await vm.ApplyTempAndReloadAsync(DateTime.UtcNow, TestContext.Current.CancellationToken);

        var last = captured.Last();
        Assert.NotNull(last.Filters);
        Assert.Equal(new[] { "Miete", "Tankstelle" }, last.Filters!.BankTextFilters);
        Assert.Empty(last.Filters!.ContactTextFilters!);
        Assert.Equal(new[] { "Miete", "Tankstelle" }, vm.SelectedBankTextFilters);
    }

    /// <summary>
    /// Clearing filters also clears the selected text filters; opening the dialog afterwards shows
    /// empty temp lists.
    /// </summary>
    [Fact]
    public void ClearFilters_ShouldClearTextFilters()
    {
        var (vm, _) = CreateVm();
        vm.OpenFilterDialog();
        vm.AddTempTextFilter(PostingKind.Bank, "Miete");
        vm.SelectedBankTextFilters.AddRange(vm.GetTempTextFilters(PostingKind.Bank));

        vm.ClearFilters();

        Assert.Empty(vm.SelectedBankTextFilters);
        vm.OpenFilterDialog();
        Assert.Empty(vm.GetTempTextFilters(PostingKind.Bank));
    }

    /// <summary>
    /// Opening the filter dialog copies the currently selected text filters into the temp buffer.
    /// </summary>
    [Fact]
    public void OpenFilterDialog_ShouldCopySelectedToTemp()
    {
        var (vm, _) = CreateVm();
        vm.SelectedSecurityTextFilters.Add("Dividende");

        vm.OpenFilterDialog();

        Assert.Equal(new[] { "Dividende" }, vm.GetTempTextFilters(PostingKind.Security));
        Assert.Equal(string.Empty, vm.NewTextFilterInput);
    }

    /// <summary>
    /// The selected/temp filter counters include text filter entries.
    /// </summary>
    [Fact]
    public void FilterCounters_ShouldIncludeTextFilters()
    {
        var (vm, _) = CreateVm();
        // counters only sum the temp/selected lists of the currently selected kinds
        vm.SelectedKinds.Add(PostingKind.Contact);
        var tempBefore = vm.GetSelectedTempFiltersCount();
        var selectedBefore = vm.GetSelectedFiltersCount();

        vm.OpenFilterDialog();
        vm.AddTempTextFilter(PostingKind.Bank, "Miete");
        vm.AddTempTextFilter(PostingKind.Contact, "Foo");

        Assert.Equal(tempBefore + 2, vm.GetSelectedTempFiltersCount());

        vm.SelectedBankTextFilters.Add("Bar");
        Assert.Equal(selectedBefore + 1, vm.GetSelectedFiltersCount());
    }

    /// <summary>
    /// Saving a favorite maps the selected text filters into the favorite filter DTO.
    /// </summary>
    [Fact]
    public async Task SaveFavorite_ShouldMapTextFilters()
    {
        var (vm, apiMock) = CreateVm();
        ReportFavoriteCreateApiRequest? captured = null;
        apiMock.Setup(a => a.Reports_CreateFavoriteAsync(It.IsAny<ReportFavoriteCreateApiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReportFavoriteCreateApiRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new ReportFavoriteDto(Guid.NewGuid(), "F", PostingKind.Bank, false, 0, 24, false, false, false, true, true, DateTime.UtcNow, null, new[] { PostingKind.Bank }, null, false));

        // the dashboard builds the filters payload from the selected lists; SaveFavoriteAsync
        // maps it onto the favorite request
        var filters = new ReportAggregatesFiltersRequest(null, null, null, null, null, null, null, null, null,
            BankTextFilters: new[] { "Miete" });
        await vm.SaveFavoriteAsync("F", PostingKind.Bank, false, 0, 24, false, false, false, true, true, new[] { PostingKind.Bank }, filters, ct: TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(new[] { "Miete" }, captured!.Filters!.BankTextFilters);
    }
}

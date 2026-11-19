using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.Reports;
using FinanceManager.Web.ViewModels.Reports;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers the kind-bound filter lifecycle of <see cref="ReportDashboardViewModel"/>:
/// <see cref="ReportDashboardViewModel.ClearKindFilters"/> drops every selection bound to a
/// removed posting kind (entity, category, subtype and text filters) so nothing stays active
/// invisibly, the filter counters only count type selections of the currently selected kinds,
/// and <see cref="ReportDashboardViewModel.SubmitFavoriteDialogAsync"/> propagates cancellation.
/// </summary>
public sealed class ReportDashboardViewModelKindFilterTests
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

    /// <summary>
    /// Clearing the filters of a removed contact kind drops its entity, category and text
    /// selections: without a filter tab they could neither be shown nor deselected, yet would
    /// still be sent and persisted into favorites.
    /// </summary>
    [Fact]
    public void ClearKindFilters_Contact_ShouldClearEntityCategoryAndTextSelections()
    {
        var (vm, _) = CreateVm();
        vm.SelectedContacts.Add(Guid.NewGuid());
        vm.SelectedContactCategories.Add(Guid.NewGuid());
        vm.SelectedContactTextFilters.Add("Miete");

        vm.ClearKindFilters(PostingKind.Contact);

        Assert.Empty(vm.SelectedContacts);
        Assert.Empty(vm.SelectedContactCategories);
        Assert.Empty(vm.SelectedContactTextFilters);
        Assert.True(vm.FiltersDirty);
    }

    /// <summary>
    /// Clearing the filters of a removed bank kind drops its account selection along with the
    /// posting-type and text filters.
    /// </summary>
    [Fact]
    public void ClearKindFilters_Bank_ShouldClearAccountTypeAndTextSelections()
    {
        var (vm, _) = CreateVm();
        vm.SelectedAccounts.Add(Guid.NewGuid());
        vm.SelectedBankPostingTypes.Add((int)BankPostingType.Inflow);
        vm.SelectedBankTextFilters.Add("Gehalt");

        vm.ClearKindFilters(PostingKind.Bank);

        Assert.Empty(vm.SelectedAccounts);
        Assert.Empty(vm.SelectedBankPostingTypes);
        Assert.Empty(vm.SelectedBankTextFilters);
        Assert.True(vm.FiltersDirty);
    }

    /// <summary>
    /// Clearing the filters of a removed security kind drops its entity, category, subtype and
    /// text selections.
    /// </summary>
    [Fact]
    public void ClearKindFilters_Security_ShouldClearEntityCategorySubtypeAndTextSelections()
    {
        var (vm, _) = CreateVm();
        vm.SelectedSecurities.Add(Guid.NewGuid());
        vm.SelectedSecurityCategories.Add(Guid.NewGuid());
        vm.SelectedSecuritySubTypes.Add((int)SecurityPostingSubType.Dividend);
        vm.SelectedSecurityTextFilters.Add("Dividende");

        vm.ClearKindFilters(PostingKind.Security);

        Assert.Empty(vm.SelectedSecurities);
        Assert.Empty(vm.SelectedSecurityCategories);
        Assert.Empty(vm.SelectedSecuritySubTypes);
        Assert.Empty(vm.SelectedSecurityTextFilters);
        Assert.True(vm.FiltersDirty);
    }

    /// <summary>
    /// Clearing the filters of a removed kind leaves the selections of the remaining kinds
    /// untouched.
    /// </summary>
    [Fact]
    public void ClearKindFilters_ShouldKeepSelectionsOfOtherKinds()
    {
        var (vm, _) = CreateVm();
        var accountId = Guid.NewGuid();
        vm.SelectedAccounts.Add(accountId);
        vm.SelectedBankPostingTypes.Add((int)BankPostingType.Inflow);
        vm.SelectedBankTextFilters.Add("Gehalt");
        vm.SelectedContacts.Add(Guid.NewGuid());

        vm.ClearKindFilters(PostingKind.Contact);

        Assert.Equal(new[] { accountId }, vm.SelectedAccounts);
        Assert.Equal(new[] { (int)BankPostingType.Inflow }, vm.SelectedBankPostingTypes);
        Assert.Equal(new[] { "Gehalt" }, vm.SelectedBankTextFilters);
    }

    /// <summary>
    /// The selected filter counter only counts bank posting types while the bank kind is
    /// selected; a stale type selection of an unselected kind must not activate the clear action.
    /// </summary>
    [Fact]
    public void GetSelectedFiltersCount_ShouldCountTypeFiltersOnlyForSelectedKinds()
    {
        var (vm, _) = CreateVm();
        // Single-kind selection (Bank); a stale security subtype (e.g. from an externally
        // created favorite) must not count.
        vm.SelectedSecuritySubTypes.Add((int)SecurityPostingSubType.Dividend);

        Assert.Equal(0, vm.GetSelectedFiltersCount());

        vm.SelectedBankPostingTypes.Add((int)BankPostingType.Inflow);
        Assert.Equal(1, vm.GetSelectedFiltersCount());
    }

    /// <summary>
    /// The temp filter counter only counts type selections of the currently selected kinds.
    /// </summary>
    [Fact]
    public void GetSelectedTempFiltersCount_ShouldCountTypeFiltersOnlyForSelectedKinds()
    {
        var (vm, _) = CreateVm();
        vm.OpenFilterDialog();
        vm.ToggleTempSecuritySubType(SecurityPostingSubType.Dividend, true);

        Assert.Equal(0, vm.GetSelectedTempFiltersCount());

        vm.ToggleTempBankPostingType(BankPostingType.Inflow, true);
        Assert.Equal(1, vm.GetSelectedTempFiltersCount());
    }

    /// <summary>
    /// In multi-kind mode the type selections of every selected kind count.
    /// </summary>
    [Fact]
    public void GetSelectedFiltersCount_MultiKind_ShouldCountTypeFiltersOfAllSelectedKinds()
    {
        var (vm, _) = CreateVm();
        vm.SelectedKinds.Add(PostingKind.Security);
        vm.SelectedSecuritySubTypes.Add((int)SecurityPostingSubType.Dividend);
        vm.SelectedBankPostingTypes.Add((int)BankPostingType.Inflow);

        Assert.Equal(2, vm.GetSelectedFiltersCount());
    }

    /// <summary>
    /// Cancelling a favorite submit propagates the <see cref="OperationCanceledException"/>
    /// instead of reporting it as a save failure.
    /// </summary>
    [Fact]
    public async Task SubmitFavoriteDialogAsync_ShouldRethrowOperationCanceledException()
    {
        var (vm, apiMock) = CreateVm();
        apiMock.Setup(a => a.Reports_CreateFavoriteAsync(It.IsAny<ReportFavoriteCreateApiRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        vm.FavoriteName = "F";
        vm.OpenFavoriteDialog(false);

        await Assert.ThrowsAsync<OperationCanceledException>(() => vm.SubmitFavoriteDialogAsync(
            "Default", PostingKind.Bank, false, 0, 24, false, false, false, true, true,
            new[] { PostingKind.Bank }, null, ct: TestContext.Current.CancellationToken));
        Assert.Null(vm.FavoriteError);
    }
}

using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.Reports;
using FinanceManager.Web.ViewModels.Reports;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers the bank posting-type filter (<see cref="ReportAggregatesFiltersRequest.BankPostingTypes"/>) of
/// <see cref="ReportDashboardViewModel"/>: saving or updating a report favorite must carry the selected
/// posting types into the <see cref="ReportFavoriteFiltersDto"/> payload so the filter survives the
/// favorite round-trip.
/// </summary>
public sealed class ReportDashboardViewModelBankPostingTypeTests
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

    private static ReportAggregatesFiltersRequest CreateFiltersWithBankPostingTypes()
        => new(null, null, null, null, null, null, null, null, null,
            BankPostingTypes: new[] { (int)BankPostingType.Inflow, (int)BankPostingType.Interest });

    /// <summary>
    /// Saving a favorite maps the selected bank posting types into the favorite filter DTO.
    /// </summary>
    [Fact]
    public async Task SaveFavorite_ShouldMapBankPostingTypes()
    {
        var (vm, apiMock) = CreateVm();
        ReportFavoriteCreateApiRequest? captured = null;
        apiMock.Setup(a => a.Reports_CreateFavoriteAsync(It.IsAny<ReportFavoriteCreateApiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReportFavoriteCreateApiRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new ReportFavoriteDto(Guid.NewGuid(), "F", PostingKind.Bank, false, 0, 24, false, false, false, true, true, DateTime.UtcNow, null, new[] { PostingKind.Bank }, null, false));

        // the dashboard builds the filters payload from the selected lists; SaveFavoriteAsync
        // maps it onto the favorite request
        await vm.SaveFavoriteAsync("F", PostingKind.Bank, false, 0, 24, false, false, false, true, true, new[] { PostingKind.Bank },
            CreateFiltersWithBankPostingTypes(), ct: TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(new[] { (int)BankPostingType.Inflow, (int)BankPostingType.Interest }, captured!.Filters!.BankPostingTypes);
    }

    /// <summary>
    /// Updating a favorite maps the selected bank posting types into the favorite filter DTO.
    /// </summary>
    [Fact]
    public async Task UpdateFavorite_ShouldMapBankPostingTypes()
    {
        var (vm, apiMock) = CreateVm();
        ReportFavoriteUpdateApiRequest? captured = null;
        apiMock.Setup(a => a.Reports_UpdateFavoriteAsync(It.IsAny<Guid>(), It.IsAny<ReportFavoriteUpdateApiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, ReportFavoriteUpdateApiRequest, CancellationToken>((_, r, _) => captured = r)
            .ReturnsAsync(new ReportFavoriteDto(Guid.NewGuid(), "F", PostingKind.Bank, false, 0, 24, false, false, false, true, true, DateTime.UtcNow, null, new[] { PostingKind.Bank }, null, false));

        await vm.UpdateFavoriteAsync(Guid.NewGuid(), "F", PostingKind.Bank, false, 0, 24, false, false, false, true, true, new[] { PostingKind.Bank },
            CreateFiltersWithBankPostingTypes(), ct: TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(new[] { (int)BankPostingType.Inflow, (int)BankPostingType.Interest }, captured!.Filters!.BankPostingTypes);
    }
}

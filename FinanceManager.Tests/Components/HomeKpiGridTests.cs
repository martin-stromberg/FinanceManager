using Bunit;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Budget;
using FinanceManager.Shared.Dtos.HomeKpi;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.Reports;
using FinanceManager.Web;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Components;

public sealed class HomeKpiGridTests : BunitContext
{
    public HomeKpiGridTests()
    {
        Services.AddLogging();
        Services.AddLocalization(options => options.ResourcesPath = "Resources");
        Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());
        Services.AddSingleton(TimeProvider.System);
    }

    [Fact]
    public void ReportFavoriteKpi_ForwardsCompareProjectionToAggregateRequest()
    {
        var kpiId = Guid.NewGuid();
        var favoriteId = Guid.NewGuid();
        var apiMock = new Mock<IApiClient>();
        ReportAggregatesQueryRequest? capturedRequest = null;

        apiMock.Setup(a => a.HomeKpis_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new HomeKpiDto(
                    kpiId,
                    HomeKpiKind.ReportFavorite,
                    favoriteId,
                    "Projected dividends",
                    null,
                    null,
                    HomeKpiDisplayMode.TotalOnly,
                    0,
                    DateTime.UtcNow,
                    null)
            });

        apiMock.Setup(a => a.Reports_GetFavoriteAsync(favoriteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReportFavoriteDto(
                favoriteId,
                "Projected dividends",
                PostingKind.Security,
                IncludeCategory: false,
                ReportInterval.Month,
                Take: 6,
                ComparePrevious: true,
                CompareYear: true,
                CompareProjection: true,
                ShowChart: true,
                Expandable: true,
                DateTime.UtcNow,
                ModifiedUtc: null,
                new[] { PostingKind.Security },
                Filters: null,
                UseValutaDate: true));

        apiMock.Setup(a => a.Reports_QueryAggregatesAsync(It.IsAny<ReportAggregatesQueryRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReportAggregatesQueryRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new ReportAggregationResult(
                ReportInterval.Month,
                new[]
                {
                    new ReportAggregatePointDto(
                        new DateTime(2026, 7, 1),
                        "Security:1",
                        "Security",
                        null,
                        10m,
                        12m,
                        null,
                        8m,
                        7m)
                },
                ComparedPrevious: true,
                ComparedYear: true,
                ComparedProjection: true));

        Services.AddSingleton(apiMock.Object);

        var cut = Render<HomeKpiGrid>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(capturedRequest);
            Assert.True(capturedRequest!.CompareProjection);
            Assert.True(capturedRequest.CompareYear);
            Assert.True(capturedRequest.UseValutaDate);
        });
    }

    [Fact]
    public void MonthlyBudgetKpi_DoesNotBlockOtherHomeKpiRendering()
    {
        var monthlyKpiId = Guid.NewGuid();
        var contactsKpiId = Guid.NewGuid();
        var pendingMonthlyKpi = new TaskCompletionSource<MonthlyBudgetKpiDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var apiMock = new Mock<IApiClient>();

        apiMock.Setup(a => a.HomeKpis_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CreatePredefinedKpi(monthlyKpiId, HomeKpiPredefined.MonthlyBudget, 0),
                CreatePredefinedKpi(contactsKpiId, HomeKpiPredefined.ContactsCount, 1)
            });
        apiMock.Setup(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()))
            .Returns(pendingMonthlyKpi.Task);
        apiMock.Setup(a => a.Contacts_CountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        Services.AddSingleton(apiMock.Object);

        var cut = Render<HomeKpiGrid>();

        cut.WaitForAssertion(() =>
        {
            apiMock.Verify(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotEmpty(cut.FindAll("a[href='/list/contacts']"));
        });
    }

    [Fact]
    public void MonthlyBudgetKpi_ReRenderDoesNotCreateSecondRequest()
    {
        var monthlyKpiId = Guid.NewGuid();
        var pendingMonthlyKpi = new TaskCompletionSource<MonthlyBudgetKpiDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var apiMock = new Mock<IApiClient>();

        apiMock.Setup(a => a.HomeKpis_ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                CreatePredefinedKpi(monthlyKpiId, HomeKpiPredefined.MonthlyBudget, 0)
            });
        apiMock.Setup(a => a.Budgets_GetMonthlyKpiAsync(
                null,
                BudgetReportDateBasis.BookingDate,
                It.IsAny<CancellationToken>()))
            .Returns(pendingMonthlyKpi.Task);

        Services.AddSingleton(apiMock.Object);

        var cut = Render<HomeKpiGrid>();
        cut.WaitForAssertion(() => apiMock.Verify(a => a.Budgets_GetMonthlyKpiAsync(
            null,
            BudgetReportDateBasis.BookingDate,
            It.IsAny<CancellationToken>()), Times.Once));

        cut.Render();

        apiMock.Verify(a => a.Budgets_GetMonthlyKpiAsync(
            null,
            BudgetReportDateBasis.BookingDate,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static HomeKpiDto CreatePredefinedKpi(Guid id, HomeKpiPredefined predefinedType, int sortOrder) =>
        new(
            id,
            HomeKpiKind.Predefined,
            null,
            null,
            null,
            predefinedType,
            HomeKpiDisplayMode.TotalOnly,
            sortOrder,
            DateTime.UtcNow,
            null);
}

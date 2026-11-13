using Bunit;
using FinanceManager.Shared.Dtos.Portfolio;
using FinanceManager.Web;
using FinanceManager.Web.Components.Pages.Portfolio;
using FinanceManager.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using static FinanceManager.Tests.TestHelpers.CultureTestHelper;

namespace FinanceManager.Tests.Components.Portfolio;

/// <summary>
/// Tests for <see cref="PortfolioDepotDevelopmentCard"/> — the dashboard tile that renders the
/// year-over-year depot development with its client-side period and yield-basis filters.
/// </summary>
public sealed class PortfolioDepotDevelopmentCardTests : BunitContext
{
    private void RegisterLocalizer()
    {
        Services.AddLocalization(options => options.ResourcesPath = "Resources");
        Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());
    }

    private static PortfolioDepotDevelopmentYear Year(int year, bool isYtd = false,
        decimal start = 1000m, decimal end = 1100m, decimal dividends = 50m, decimal capital = 800m)
        => new(year, isYtd, start, end, dividends, capital);

    /// <summary>
    /// Switching the yield-basis dropdown recalculates the ratio and yield columns client-side:
    /// with invested capital as basis the yield rises relative to the year-start basis.
    /// </summary>
    [Fact]
    public void YieldBasisSwitch_UpdatesRatioAndYieldColumns()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var data = new PortfolioDepotDevelopmentDto([Year(2023)]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            var yieldBefore = cut.FindAll("tbody tr td")[6].TextContent;
            cut.Find("select#dev-yield-basis").Change("InvestedCapital");
            var yieldAfter = cut.FindAll("tbody tr td")[6].TextContent;

            Assert.Equal("+5.00%", yieldBefore);
            Assert.Equal("+6.25%", yieldAfter);
            Assert.Contains("800 €", cut.FindAll("tbody tr td")[5].TextContent);
        });
    }

    /// <summary>
    /// The period filter limits the rendered rows to the selected number of trailing years
    /// anchored to the last year of the data series.
    /// </summary>
    [Fact]
    public void PeriodFilter_LimitsRenderedRows()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var currentYear = DateTime.UtcNow.Year;
            var data = new PortfolioDepotDevelopmentDto(
                Enumerable.Range(currentYear - 6, 7).Select(y => Year(y)).ToList());
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            Assert.Equal(7, cut.FindAll("tbody tr").Count);

            cut.Find("select#dev-period-filter").Change("Last5Years");

            Assert.Equal(5, cut.FindAll("tbody tr").Count);
        });
    }

    /// <summary>
    /// The "last N years" period filter is anchored to the last year of the data series, not the
    /// current calendar year — a depot history that ended years ago (e.g. after a complete sale)
    /// still shows its final years instead of an empty result.
    /// </summary>
    [Fact]
    public void PeriodFilter_AnchoredAtLastSeriesYear()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var data = new PortfolioDepotDevelopmentDto(
                Enumerable.Range(2015, 6).Select(y => Year(y)).ToList());
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            cut.Find("select#dev-period-filter").Change("Last5Years");

            var rows = cut.FindAll("tbody tr");
            Assert.Equal(5, rows.Count);
            Assert.Equal("2016", rows[0].QuerySelector("td")!.TextContent);
            Assert.Equal("2020", rows[^1].QuerySelector("td")!.TextContent);
        });
    }

    /// <summary>
    /// A year flagged as YTD renders an asterisk marker in the table and chart label, and the
    /// explanatory footnote below the table mentions YTD.
    /// </summary>
    [Fact]
    public void YtdRow_RendersAsteriskAndFootnote()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var currentYear = DateTime.UtcNow.Year;
            var data = new PortfolioDepotDevelopmentDto(
            [
                Year(currentYear - 1),
                Year(currentYear, isYtd: true)
            ]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            var rows = cut.FindAll("tbody tr");
            Assert.Equal($"{currentYear}*", rows[1].QuerySelector("td")!.TextContent);
            Assert.Contains("YTD", cut.Find(".portfolio-chart-footnote").TextContent);
        });
    }

    /// <summary>
    /// Without any yearly rows the card renders its localized empty-state text instead of
    /// the chart and table.
    /// </summary>
    [Fact]
    public void EmptyYears_RendersEmptyState()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var data = new PortfolioDepotDevelopmentDto([]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            Assert.Equal("No security postings available yet.", cut.Find(".mini-bar-chart-empty").TextContent);
            Assert.Empty(cut.FindAll("tbody tr"));
        });
    }

    /// <summary>
    /// When the selected yield basis is zero the ratio and yield cells render a dash instead
    /// of dividing by zero.
    /// </summary>
    [Fact]
    public void ZeroBasis_RendersDashInsteadOfYield()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var data = new PortfolioDepotDevelopmentDto([Year(2023, start: 0m, capital: 0m)]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            var cells = cut.FindAll("tbody tr td");
            Assert.Equal("—", cells[5].TextContent);
            Assert.Equal("—", cells[6].TextContent);
        });
    }

    /// <summary>
    /// Nullable cells that render a dash (basis zero, value not computable) must not carry the
    /// negative styling class — a non-computable value is neutral, not a loss.
    /// </summary>
    [Fact]
    public void ZeroBasis_DashCells_RenderWithoutNegativeClass()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var data = new PortfolioDepotDevelopmentDto([Year(2023, start: 0m, capital: 0m)]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            var cells = cut.FindAll("tbody tr td");
            Assert.Equal("—", cells[3].TextContent);
            Assert.Equal("—", cells[6].TextContent);
            Assert.DoesNotContain("negative", cells[3].GetAttribute("class") ?? string.Empty);
            Assert.DoesNotContain("negative", cells[6].GetAttribute("class") ?? string.Empty);
        });
    }

    /// <summary>
    /// Monetary values are rendered culture-aware as rounded euro amounts and the derived
    /// change, ratio and yield columns use signed percent formatting.
    /// </summary>
    [Fact]
    public void Formatting_CurrencyAndSignedPercent()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var data = new PortfolioDepotDevelopmentDto([Year(2023)]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            var cells = cut.FindAll("tbody tr td");
            Assert.Equal("2023", cells[0].TextContent);
            Assert.Equal("1,100 €", cells[1].TextContent);
            Assert.Equal("+100 €", cells[2].TextContent);
            Assert.Equal("+10.00%", cells[3].TextContent);
            Assert.Equal("50 €", cells[4].TextContent);
            Assert.Equal("50 € / 1,000 €", cells[5].TextContent);
            Assert.Equal("+5.00%", cells[6].TextContent);
        });
    }
}

using Bunit;
using FinanceManager.Shared.Dtos.Portfolio;
using FinanceManager.Tests.TestHelpers;
using FinanceManager.Web;
using FinanceManager.Web.Components.Pages.Portfolio;
using FinanceManager.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using static FinanceManager.Tests.TestHelpers.CultureTestHelper;

namespace FinanceManager.Tests.Components.Portfolio;

/// <summary>
/// Tests for the YTD explanation footnote shown below the annual-returns <c>MiniBarChart</c> in
/// <see cref="PortfolioPerformanceCard"/>. The chart marks the current, not-yet-completed year with a "*"
/// suffix on its bar label; the footnote ties that symbol back to the same YTD concept already explained
/// via the localized "(YTD)" suffix used in the table below the chart.
/// </summary>
public sealed class PortfolioPerformanceCardTests_YtdFootnote : BunitContext
{
    private static PortfolioPerformanceDto BuildData(IReadOnlyList<PortfolioAnnualReturnPoint> annualReturns)
        => new(null, null, annualReturns, []);

    /// <summary>
    /// When one of the annual return points represents the current, not-yet-completed year (<c>IsYtd</c>),
    /// the footnote explaining the chart's "*" marker must be rendered below the chart.
    /// </summary>
    [Fact]
    public void AnnualReturns_ContainsYtdPoint_RendersFootnoteExplainingAsterisk()
    {
        WithInvariantCulture(() =>
        {
            Services.AddLocalization(options => options.ResourcesPath = "Resources");
            Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());

            var data = BuildData(
            [
                new PortfolioAnnualReturnPoint(2023, 0.05m, false),
                new PortfolioAnnualReturnPoint(2024, 0.08m, true)
            ]);

            var cut = Render<PortfolioPerformanceCard>(builder => builder.Add(c => c.Data, data));

            var footnote = cut.Find(".portfolio-chart-footnote");
            Assert.Contains("YTD", footnote.TextContent);
        });
    }

    /// <summary>
    /// When none of the annual return points is the current year (no <c>IsYtd</c> point), the "*" marker
    /// never appears in the chart, so the explanatory footnote must not be rendered either.
    /// </summary>
    [Fact]
    public void AnnualReturns_NoYtdPoint_DoesNotRenderFootnote()
    {
        WithInvariantCulture(() =>
        {
            Services.AddLocalization(options => options.ResourcesPath = "Resources");
            Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());

            var data = BuildData(
            [
                new PortfolioAnnualReturnPoint(2022, 0.03m, false),
                new PortfolioAnnualReturnPoint(2023, 0.05m, false)
            ]);

            var cut = Render<PortfolioPerformanceCard>(builder => builder.Add(c => c.Data, data));

            Assert.Empty(cut.FindAll(".portfolio-chart-footnote"));
        });
    }

    /// <summary>
    /// When there are no annual return points at all, neither the chart nor the footnote is rendered.
    /// </summary>
    [Fact]
    public void AnnualReturns_Empty_DoesNotRenderChartOrFootnote()
    {
        WithInvariantCulture(() =>
        {
            Services.AddLocalization(options => options.ResourcesPath = "Resources");
            Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());

            var data = BuildData([]);

            var cut = Render<PortfolioPerformanceCard>(builder => builder.Add(c => c.Data, data));

            Assert.Empty(cut.FindAll(".mini-bar-chart"));
            Assert.Empty(cut.FindAll(".portfolio-chart-footnote"));
        });
    }
}

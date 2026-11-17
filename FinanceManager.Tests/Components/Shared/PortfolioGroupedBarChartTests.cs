using Bunit;
using FinanceManager.Web.Components.Shared;
using static FinanceManager.Tests.TestHelpers.CultureTestHelper;

namespace FinanceManager.Tests.Components.Shared;

/// <summary>
/// Tests for <see cref="PortfolioGroupedBarChart"/> — the grouped SVG bar chart rendering two bars
/// per data point (used by the depot development tile's invested-capital view).
/// </summary>
public sealed class PortfolioGroupedBarChartTests : BunitContext
{
    /// <summary>
    /// Each data point renders exactly two bars (first and second series) plus one
    /// <c>&lt;title&gt;</c> tooltip produced by the configured <c>TooltipFormatter</c>.
    /// </summary>
    [Fact]
    public void Render_Points_EmitsTwoBarsAndTitleTooltipPerPoint()
    {
        WithInvariantCulture(() =>
        {
            var points = new List<PortfolioGroupedBarChart.PortfolioGroupedBarChartPoint>
            {
                new("2023", 1000m, 50m),
                new("2024", -200m, 60m)
            };

            var cut = Render<PortfolioGroupedBarChart>(builder => builder
                .Add(c => c.Points, points)
                .Add(c => c.TooltipFormatter, p => $"{p.Label}|{p.FirstValue}|{p.SecondValue}"));

            Assert.Equal(4, cut.FindAll("rect.portfolio-grouped-bar-chart-bar").Count);
            Assert.Equal(2, cut.FindAll("rect.portfolio-grouped-bar-chart-bar.first").Count);
            Assert.Equal(2, cut.FindAll("rect.portfolio-grouped-bar-chart-bar.second").Count);
            Assert.Single(cut.FindAll("rect.portfolio-grouped-bar-chart-bar.negative"));

            var titles = cut.FindAll("g.portfolio-grouped-bar-chart-point title");
            Assert.Equal("2023|1000|50", titles[0].TextContent);
            Assert.Equal("2024|-200|60", titles[1].TextContent);
        });
    }

    /// <summary>
    /// Both series share one scale so the largest value across both reaches the chart top
    /// and a zero value renders an empty-height bar at the baseline.
    /// </summary>
    [Fact]
    public void Render_BothSeries_ShareSingleScale()
    {
        WithInvariantCulture(() =>
        {
            var points = new List<PortfolioGroupedBarChart.PortfolioGroupedBarChartPoint>
            {
                new("2023", 100m, 25m)
            };

            var cut = Render<PortfolioGroupedBarChart>(builder => builder
                .Add(c => c.Points, points));

            var first = cut.Find("rect.portfolio-grouped-bar-chart-bar.first");
            var second = cut.Find("rect.portfolio-grouped-bar-chart-bar.second");

            // Max value (100) spans the full drawing height (300 - 20 = 280).
            Assert.Equal("280.00", first.GetAttribute("height"));
            // The smaller bar takes exactly a quarter of that height on the shared scale.
            Assert.Equal("70.00", second.GetAttribute("height"));
        });
    }

    /// <summary>
    /// The svg exposes the configured accessible label and the legend renders both configured
    /// series labels.
    /// </summary>
    [Fact]
    public void Render_Always_ShowsLegendAndAriaLabel()
    {
        WithInvariantCulture(() =>
        {
            var cut = Render<PortfolioGroupedBarChart>(builder => builder
                .Add(c => c.Points, new[] { new PortfolioGroupedBarChart.PortfolioGroupedBarChartPoint("2023", 100m, 10m) })
                .Add(c => c.AriaLabel, "chart aria")
                .Add(c => c.FirstLabel, "Deposits/withdrawals")
                .Add(c => c.SecondLabel, "Net dividends"));

            Assert.Equal("chart aria", cut.Find("svg").GetAttribute("aria-label"));

            var legend = cut.Find(".portfolio-grouped-bar-chart-legend");
            Assert.Contains("Deposits/withdrawals", legend.TextContent);
            Assert.Contains("Net dividends", legend.TextContent);
        });
    }

    /// <summary>
    /// With no points the configured empty text is rendered and no bars exist.
    /// </summary>
    [Fact]
    public void Render_NoPoints_RendersEmptyText()
    {
        WithInvariantCulture(() =>
        {
            var cut = Render<PortfolioGroupedBarChart>(builder => builder
                .Add(c => c.Points, Array.Empty<PortfolioGroupedBarChart.PortfolioGroupedBarChartPoint>())
                .Add(c => c.EmptyText, "No data"));

            Assert.Equal("No data", cut.Find(".portfolio-grouped-bar-chart-empty").TextContent);
            Assert.Empty(cut.FindAll("rect.portfolio-grouped-bar-chart-bar"));
        });
    }
}

using Bunit;
using FinanceManager.Web.Components.Shared;
using static FinanceManager.Tests.TestHelpers.CultureTestHelper;

namespace FinanceManager.Tests.Components.Shared;

/// <summary>
/// Tests for <see cref="PortfolioBarLineChart"/> — the combined SVG line-and-bar chart used by the
/// depot development tile. Covers series scaling, tooltips, legend, accessibility label and empty state.
/// </summary>
public sealed class PortfolioBarLineChartTests : BunitContext
{
    /// <summary>
    /// Each data point renders an SVG <c>&lt;title&gt;</c> tooltip whose content is produced by the
    /// configured <c>TooltipFormatter</c>.
    /// </summary>
    [Fact]
    public void Render_Points_EmitsTitleTooltipWithFormattedValues()
    {
        WithInvariantCulture(() =>
        {
            var points = new List<PortfolioBarLineChart.PortfolioBarLineChartPoint>
            {
                new("2023", 1000m, 50m),
                new("2024", 1200m, 60m)
            };

            var cut = Render<PortfolioBarLineChart>(builder => builder
                .Add(c => c.Points, points)
                .Add(c => c.TooltipFormatter, p => $"{p.Label}|{p.LineValue}|{p.BarValue}"));

            var titles = cut.FindAll("g.portfolio-bar-line-chart-point title");
            Assert.Equal(2, titles.Count);
            Assert.Equal("2023|1000|50", titles[0].TextContent);
            Assert.Equal("2024|1200|60", titles[1].TextContent);
        });
    }

    /// <summary>
    /// The line series is normalized over the full drawing height while the bars are scaled
    /// independently inside the lower chart area, so a maximal line value reaches the top of the
    /// chart while a maximal bar value fills the whole bar area.
    /// </summary>
    [Fact]
    public void Render_LineAndBars_ScaledIndependently()
    {
        WithInvariantCulture(() =>
        {
            var points = new List<PortfolioBarLineChart.PortfolioBarLineChartPoint>
            {
                new("2023", 100m, 10m),
                new("2024", 200m, 20m)
            };

            var cut = Render<PortfolioBarLineChart>(builder => builder
                .Add(c => c.Points, points));

            var dots = cut.FindAll("circle.portfolio-bar-line-chart-dot");
            // Line max (200) maps to the chart top y=20.
            Assert.Equal("20.00", dots[1].GetAttribute("cy"));

            var bars = cut.FindAll("rect.portfolio-bar-line-chart-bar");
            // Bar max (20) fills the whole bar area height (300 - 160 = 140).
            Assert.Equal("140.00", bars[1].GetAttribute("height"));
        });
    }

    /// <summary>
    /// The svg exposes the configured accessible label and the legend always renders both
    /// configured series labels.
    /// </summary>
    [Fact]
    public void Render_Always_ShowsLegendAndAriaLabel()
    {
        WithInvariantCulture(() =>
        {
            var cut = Render<PortfolioBarLineChart>(builder => builder
                .Add(c => c.Points, Array.Empty<PortfolioBarLineChart.PortfolioBarLineChartPoint>())
                .Add(c => c.AriaLabel, "chart aria")
                .Add(c => c.LineLabel, "Depot value")
                .Add(c => c.BarLabel, "Net dividends"));

            var legend = cut.Find(".portfolio-bar-line-chart-legend");
            Assert.Contains("Depot value", legend.TextContent);
            Assert.Contains("Net dividends", legend.TextContent);

            cut = Render<PortfolioBarLineChart>(builder => builder
                .Add(c => c.Points, new[] { new PortfolioBarLineChart.PortfolioBarLineChartPoint("2023", 100m, 10m) })
                .Add(c => c.AriaLabel, "chart aria"));

            Assert.Equal("chart aria", cut.Find("svg").GetAttribute("aria-label"));
        });
    }

    /// <summary>
    /// With no points the configured empty text is rendered and no bars, dots or line path exist.
    /// </summary>
    [Fact]
    public void Render_NoPoints_RendersEmptyText()
    {
        WithInvariantCulture(() =>
        {
            var cut = Render<PortfolioBarLineChart>(builder => builder
                .Add(c => c.Points, Array.Empty<PortfolioBarLineChart.PortfolioBarLineChartPoint>())
                .Add(c => c.EmptyText, "No data"));

            Assert.Equal("No data", cut.Find(".portfolio-bar-line-chart-empty").TextContent);
            Assert.Empty(cut.FindAll("rect.portfolio-bar-line-chart-bar"));
            Assert.Empty(cut.FindAll("circle.portfolio-bar-line-chart-dot"));
        });
    }
}

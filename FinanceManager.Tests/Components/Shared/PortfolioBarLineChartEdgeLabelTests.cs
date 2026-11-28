using Bunit;
using FinanceManager.Web.Components.Shared;

namespace FinanceManager.Tests.Components.Shared;

/// <summary>
/// Covers the x-axis label placement and bar extents of <see cref="PortfolioBarLineChart"/>
/// (#610, same defect pattern as <see cref="BudgetLineChart"/>): edge labels are clamped into
/// the viewBox and edge bars no longer overflow the plot area.
/// </summary>
public sealed class PortfolioBarLineChartEdgeLabelTests : Bunit.BunitContext
{
    // mirrors the conservative width estimate used by the component (18px font, ~0.58em/char)
    private const double EstimatedWidthPerChar = 10.5;

    private static IReadOnlyList<PortfolioBarLineChart.PortfolioBarLineChartPoint> Points(int count)
        => Enumerable.Range(0, count)
            .Select(i => new PortfolioBarLineChart.PortfolioBarLineChartPoint($"Y{i}", 10m + i, 5m + i))
            .ToList();

    private static double X(AngleSharp.Dom.IElement element)
        => double.Parse(element.GetAttribute("x")!, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Every drawn label keeps its estimated extent inside the 1000-unit viewBox while staying
    /// middle-anchored — the label midpoint is clamped instead of re-anchored.
    /// </summary>
    [Fact]
    public void Labels_StayInsideViewBox()
    {
        var cut = Render<PortfolioBarLineChart>(p => p.Add(c => c.Points, Points(6)));

        var labels = cut.FindAll(".portfolio-bar-line-chart-labels text");
        Assert.Equal(6, labels.Count);
        foreach (var label in labels)
        {
            Assert.Equal("middle", label.GetAttribute("text-anchor"));
            var half = label.TextContent.Length * EstimatedWidthPerChar / 2;
            Assert.True(X(label) - half >= 0, $"label '{label.TextContent}' overflows the left edge (x={label.GetAttribute("x")})");
            Assert.True(X(label) + half <= 1000, $"label '{label.TextContent}' overflows the right edge (x={label.GetAttribute("x")})");
        }
    }

    /// <summary>
    /// With few points the bar width cap (60 units) would push the first and last bars past the
    /// plot edges — the bars are clamped into [60, 980] instead.
    /// </summary>
    [Fact]
    public void EdgeBars_StayInsidePlotArea()
    {
        var cut = Render<PortfolioBarLineChart>(p => p.Add(c => c.Points, Points(5)));

        var bars = cut.FindAll("rect.portfolio-bar-line-chart-bar");
        Assert.Equal(5, bars.Count);
        foreach (var bar in bars)
        {
            var width = double.Parse(bar.GetAttribute("width")!, System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(X(bar) >= 60, $"bar starts left of the plot area (x={bar.GetAttribute("x")})");
            Assert.True(X(bar) + width <= 980, $"bar ends right of the plot area (x={bar.GetAttribute("x")}, w={width})");
        }
    }

    /// <summary>
    /// A single point is rendered in the chart center — its label stays middle-anchored.
    /// </summary>
    [Fact]
    public void SinglePoint_LabelStaysMiddleAnchored()
    {
        var cut = Render<PortfolioBarLineChart>(p => p.Add(c => c.Points, Points(1)));

        var label = Assert.Single(cut.FindAll(".portfolio-bar-line-chart-labels text"));
        Assert.Equal("middle", label.GetAttribute("text-anchor"));
        Assert.Equal("520.00", label.GetAttribute("x"));
    }

    /// <summary>
    /// The label baseline leaves headroom for descenders inside the 325-unit viewBox.
    /// </summary>
    [Fact]
    public void Labels_BaselineInsideViewBox()
    {
        var cut = Render<PortfolioBarLineChart>(p => p.Add(c => c.Points, Points(2)));

        foreach (var label in cut.FindAll(".portfolio-bar-line-chart-labels text"))
        {
            Assert.Equal("318.00", label.GetAttribute("y"));
        }
    }
}

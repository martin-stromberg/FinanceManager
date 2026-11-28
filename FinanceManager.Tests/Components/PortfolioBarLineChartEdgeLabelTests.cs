using Bunit;
using FinanceManager.Web.Components.Shared;

namespace FinanceManager.Tests.Components;

/// <summary>
/// Covers the x-axis label anchoring of <see cref="PortfolioBarLineChart"/> (#610, same defect
/// pattern as <see cref="BudgetLineChart"/>): the first and last labels must anchor inward so
/// they stay fully inside the viewBox.
/// </summary>
public sealed class PortfolioBarLineChartEdgeLabelTests : Bunit.BunitContext
{
    private static IReadOnlyList<PortfolioBarLineChart.PortfolioBarLineChartPoint> Points(int count)
        => Enumerable.Range(0, count)
            .Select(i => new PortfolioBarLineChart.PortfolioBarLineChartPoint($"Y{i}", 10m + i, 5m + i))
            .ToList();


    /// <summary>
    /// With two or more points the first label is start-anchored and the last label is
    /// end-anchored so both stay fully inside the viewBox.
    /// </summary>
    [Fact]
    public void EdgeLabels_AnchorInward()
    {
        var cut = Render<PortfolioBarLineChart>(p => p.Add(c => c.Points, Points(6)));

        var labels = cut.FindAll(".portfolio-bar-line-chart-labels text");
        Assert.Equal(6, labels.Count);
        Assert.Equal("start", labels[0].GetAttribute("text-anchor"));
        Assert.Equal("end", labels[^1].GetAttribute("text-anchor"));
        foreach (var middle in labels.Skip(1).SkipLast(1))
        {
            Assert.Equal("middle", middle.GetAttribute("text-anchor"));
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

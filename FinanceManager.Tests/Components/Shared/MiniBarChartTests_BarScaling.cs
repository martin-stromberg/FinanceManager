using Bunit;
using FinanceManager.Web.Components.Shared;

namespace FinanceManager.Tests.Components.Shared;

/// <summary>
/// Tests for <see cref="MiniBarChart"/>'s bar-height scaling, which maps positive values into the upper
/// half of the available height and negative values into the lower half, mirrored around a zero line.
/// </summary>
public sealed class MiniBarChartTests_BarScaling : BunitContext
{
    private static int ParseHeightPx(string style)
    {
        var start = style.IndexOf("height:", StringComparison.Ordinal) + "height:".Length;
        var end = style.IndexOf("px", start, StringComparison.Ordinal);
        return int.Parse(style[start..end]);
    }

    /// <summary>
    /// A positive value equal to the largest absolute value in the series fills exactly half of the
    /// configured total height (the positive row).
    /// </summary>
    [Fact]
    public void Render_LargestPositiveValue_FillsHalfOfHeightPx()
    {
        var points = new List<MiniBarChart.MiniBarChartPoint> { new("Y1", 10m) };

        var cut = Render<MiniBarChart>(builder => builder
            .Add(c => c.Points, points)
            .Add(c => c.HeightPx, 100));

        var bar = cut.Find(".mini-bar-chart-bar.positive");
        Assert.Equal(50, ParseHeightPx(bar.GetAttribute("style")!));
    }

    /// <summary>
    /// A negative value equal in magnitude to the largest absolute value fills half of the height in the
    /// negative row, while the corresponding positive row stays at zero height.
    /// </summary>
    [Fact]
    public void Render_LargestNegativeValue_FillsHalfOfHeightPxInNegativeRow()
    {
        var points = new List<MiniBarChart.MiniBarChartPoint> { new("Y1", -10m) };

        var cut = Render<MiniBarChart>(builder => builder
            .Add(c => c.Points, points)
            .Add(c => c.HeightPx, 100));

        var negativeBar = cut.Find(".mini-bar-chart-bar.negative");
        var positiveBar = cut.Find(".mini-bar-chart-bar.positive");
        Assert.Equal(50, ParseHeightPx(negativeBar.GetAttribute("style")!));
        Assert.Equal(0, ParseHeightPx(positiveBar.GetAttribute("style")!));
    }

    /// <summary>
    /// A smaller value is scaled proportionally relative to the largest absolute value in the series.
    /// </summary>
    [Fact]
    public void Render_SmallerValueRelativeToMax_ScalesProportionally()
    {
        var points = new List<MiniBarChart.MiniBarChartPoint>
        {
            new("Y1", 10m),
            new("Y2", 5m)
        };

        var cut = Render<MiniBarChart>(builder => builder
            .Add(c => c.Points, points)
            .Add(c => c.HeightPx, 100));

        var bars = cut.FindAll(".mini-bar-chart-bar.positive");
        Assert.Equal(50, ParseHeightPx(bars[0].GetAttribute("style")!));
        Assert.Equal(25, ParseHeightPx(bars[1].GetAttribute("style")!));
    }

    /// <summary>
    /// When every value is zero, the scaling factor is zero (avoiding division by zero) and every bar
    /// renders with zero height instead of throwing.
    /// </summary>
    [Fact]
    public void Render_AllValuesZero_RendersZeroHeightBarsWithoutThrowing()
    {
        var points = new List<MiniBarChart.MiniBarChartPoint> { new("Y1", 0m) };

        var cut = Render<MiniBarChart>(builder => builder
            .Add(c => c.Points, points)
            .Add(c => c.HeightPx, 100));

        var positiveBar = cut.Find(".mini-bar-chart-bar.positive");
        var negativeBar = cut.Find(".mini-bar-chart-bar.negative");
        Assert.Equal(0, ParseHeightPx(positiveBar.GetAttribute("style")!));
        Assert.Equal(0, ParseHeightPx(negativeBar.GetAttribute("style")!));
    }

    /// <summary>
    /// With no points at all, the configured empty-state text is rendered instead of any bars.
    /// </summary>
    [Fact]
    public void Render_NoPoints_RendersEmptyText()
    {
        var cut = Render<MiniBarChart>(builder => builder
            .Add(c => c.Points, Array.Empty<MiniBarChart.MiniBarChartPoint>())
            .Add(c => c.EmptyText, "No data"));

        Assert.Equal("No data", cut.Find(".mini-bar-chart-empty").TextContent);
        Assert.Empty(cut.FindAll(".mini-bar-chart-bar"));
    }
}

using Bunit;
using FinanceManager.Web.Components.Shared;

namespace FinanceManager.Tests.Components.Shared;

/// <summary>
/// Covers the x-axis label placement of <see cref="BudgetLineChart"/> (#610): edge labels must
/// stay fully inside the viewBox — previously all labels were centered on their data point and
/// the last label was clipped at the right SVG edge.
/// </summary>
public sealed class BudgetLineChartEdgeLabelTests : Bunit.BunitContext
{
    // mirrors the conservative width estimate used by the component (18px font, ~0.58em/char)
    private const double EstimatedWidthPerChar = 10.5;

    private static IReadOnlyList<(DateOnly PeriodStart, decimal Budget, decimal Actual)> Data(int count)
    {
        var start = new DateOnly(2026, 1, 1);
        return Enumerable.Range(0, count)
            .Select(i => (start.AddMonths(i), 100m + i, 90m + i))
            .ToList();
    }

    private static double X(AngleSharp.Dom.IElement label)
        => double.Parse(label.GetAttribute("x")!, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Every drawn label keeps its estimated extent inside the 1000-unit viewBox — the left edge
    /// never goes negative and the right edge never passes the boundary.
    /// </summary>
    /// <param name="count">Number of monthly data points rendered into the chart.</param>
    [Theory]
    [InlineData(2)]
    [InlineData(12)]
    [InlineData(24)]
    public void Labels_StayInsideViewBox(int count)
    {
        var cut = Render<BudgetLineChart>(p => p.Add(c => c.Data, Data(count)));

        foreach (var label in cut.FindAll(".labels text"))
        {
            var half = label.TextContent.Length * EstimatedWidthPerChar / 2;
            Assert.True(X(label) - half >= 0, $"label '{label.TextContent}' overflows the left edge (x={label.GetAttribute("x")})");
            Assert.True(X(label) + half <= 1000, $"label '{label.TextContent}' overflows the right edge (x={label.GetAttribute("x")})");
        }
    }

    /// <summary>
    /// The first label still sits centered on its data point — the 60-unit left margin is always
    /// wide enough, so no displacement is applied there.
    /// </summary>
    [Fact]
    public void FirstLabel_StaysCenteredOnDataPoint()
    {
        var cut = Render<BudgetLineChart>(p => p.Add(c => c.Data, Data(12)));

        var first = cut.FindAll(".labels text")[0];
        Assert.Equal("middle", first.GetAttribute("text-anchor"));
        Assert.Equal("60.00", first.GetAttribute("x"));
    }

    /// <summary>
    /// With more than 12 points the last data point's label is drawn in addition to the step grid;
    /// a stepped predecessor that would collide with it is dropped instead of overlapping.
    /// </summary>
    [Fact]
    public void SteppedLabels_PredecessorCollidingWithEndLabel_IsDropped()
    {
        var cut = Render<BudgetLineChart>(p => p.Add(c => c.Data, Data(24)));

        var labels = cut.FindAll(".labels text");
        var last = labels[^1];
        var previous = labels[^2];

        // the last label is drawn clamped to the right edge...
        var lastHalf = last.TextContent.Length * EstimatedWidthPerChar / 2;
        Assert.True(X(last) + lastHalf <= 1000, "last label overflows the right edge");

        // ...and keeps a collision-free distance to the previous drawn label
        var minGap = (previous.TextContent.Length * EstimatedWidthPerChar + last.TextContent.Length * EstimatedWidthPerChar) / 2;
        Assert.True(X(last) - X(previous) >= minGap, $"last two labels overlap (gap={X(last) - X(previous)})");
    }

    /// <summary>
    /// A single data point is rendered in the chart center — its label stays middle-anchored.
    /// </summary>
    [Fact]
    public void SinglePoint_LabelStaysMiddleAnchored()
    {
        var cut = Render<BudgetLineChart>(p => p.Add(c => c.Data, Data(1)));

        var label = Assert.Single(cut.FindAll(".labels text"));
        Assert.Equal("middle", label.GetAttribute("text-anchor"));
        Assert.Equal("520.00", label.GetAttribute("x"));
    }

    /// <summary>
    /// The label baseline leaves headroom for descenders inside the 325-unit viewBox.
    /// </summary>
    [Fact]
    public void Labels_BaselineInsideViewBox()
    {
        var cut = Render<BudgetLineChart>(p => p.Add(c => c.Data, Data(2)));

        foreach (var label in cut.FindAll(".labels text"))
        {
            Assert.Equal("318.00", label.GetAttribute("y"));
            Assert.Equal("middle", label.GetAttribute("text-anchor"));
        }
    }
}

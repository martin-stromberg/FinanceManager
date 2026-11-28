using Bunit;
using FinanceManager.Web.Components.Shared;

namespace FinanceManager.Tests.Components;

/// <summary>
/// Covers the x-axis label anchoring of <see cref="BudgetLineChart"/> (#610): edge labels must
/// anchor inward so the first and last label can never overflow the viewBox — previously all
/// labels were middle-anchored and the last label was clipped at the right SVG edge.
/// </summary>
public sealed class BudgetLineChartTests : Bunit.BunitContext
{
    private static IReadOnlyList<(DateOnly PeriodStart, decimal Budget, decimal Actual)> Data(int count)
    {
        var start = new DateOnly(2026, 1, 1);
        return Enumerable.Range(0, count)
            .Select(i => (start.AddMonths(i), 100m + i, 90m + i))
            .ToList();
    }


    /// <summary>
    /// With two or more data points the first label is start-anchored and the last label is
    /// end-anchored so both stay fully inside the viewBox.
    /// </summary>
    [Fact]
    public void EdgeLabels_AnchorInward()
    {
        var cut = Render<BudgetLineChart>(p => p.Add(c => c.Data, Data(12)));

        var labels = cut.FindAll(".labels text");
        Assert.Equal(12, labels.Count);
        Assert.Equal("start", labels[0].GetAttribute("text-anchor"));
        Assert.Equal("end", labels[^1].GetAttribute("text-anchor"));
        foreach (var middle in labels.Skip(1).SkipLast(1))
        {
            Assert.Equal("middle", middle.GetAttribute("text-anchor"));
        }
    }

    /// <summary>
    /// With more than 12 points the step logic keeps the edge-anchor rule: the first and last
    /// drawn labels still anchor inward while intermediate labels stay centered.
    /// </summary>
    [Fact]
    public void EdgeLabels_SteppedLabels_KeepInwardAnchors()
    {
        var cut = Render<BudgetLineChart>(p => p.Add(c => c.Data, Data(24)));

        var labels = cut.FindAll(".labels text");
        Assert.Equal("start", labels[0].GetAttribute("text-anchor"));
        Assert.Equal("end", labels[^1].GetAttribute("text-anchor"));
        foreach (var middle in labels.Skip(1).SkipLast(1))
        {
            Assert.Equal("middle", middle.GetAttribute("text-anchor"));
        }
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
            Assert.Equal("318", label.GetAttribute("y"));
        }
    }
}

using Bunit;
using FinanceManager.Shared.Dtos.Portfolio;
using FinanceManager.Web.Components.Shared;
using static FinanceManager.Tests.TestHelpers.CultureTestHelper;

namespace FinanceManager.Tests.Components.Shared;

/// <summary>
/// Tests for <see cref="PositionsExplanationTable"/> rendered in isolation (without going through
/// <see cref="FinanceManager.Web.Components.Pages.Portfolio.PortfolioStructureCard"/>), covering the
/// "and N more" cap, the empty-list case and value colorization.
/// </summary>
public sealed class PositionsExplanationTableTests : BunitContext
{
    private static PortfolioTopPosition Position(string name, decimal value)
        => new(Guid.NewGuid(), name, value, 0m, value);

    private IRenderedComponent<PositionsExplanationTable> RenderTable(IReadOnlyList<PortfolioTopPosition> positions, decimal totalValue, int? maxListEntries = null, bool colorizePositiveNegative = false)
        => Render<PositionsExplanationTable>(builder =>
        {
            builder.Add(c => c.Positions, positions);
            if (maxListEntries.HasValue) { builder.Add(c => c.MaxListEntries, maxListEntries.Value); }
            builder.Add(c => c.NameColumnHeader, "Name");
            builder.Add(c => c.ValueColumnHeader, "Value");
            builder.Add(c => c.ValueSelector, (Func<PortfolioTopPosition, decimal>)(pos => pos.MarketValue));
            builder.Add(c => c.TotalLabel, "Total");
            builder.Add(c => c.TotalValue, totalValue);
            builder.Add(c => c.ColorizePositiveNegative, colorizePositiveNegative);
            builder.Add(c => c.MoreEntriesText, (Func<int, string>)(n => $"and {n} more"));
        });

    /// <summary>
    /// With an empty position list, only the total row is rendered — no position rows and no "and N more" row.
    /// </summary>
    [Fact]
    public void Render_EmptyPositions_RendersOnlyTotalRow()
    {
        WithInvariantCulture(() =>
        {
            var cut = RenderTable([], 0m);

            var rows = cut.FindAll("tbody tr");
            Assert.Single(rows);
            Assert.Contains("Total", rows[0].TextContent);
        });
    }

    /// <summary>
    /// When the position count exceeds <c>MaxListEntries</c>, rendering caps at that many rows and adds
    /// an "and N more" row for the remainder.
    /// </summary>
    [Fact]
    public void Render_MoreThanMaxListEntries_CapsRowsAndShowsMoreEntriesRow()
    {
        WithInvariantCulture(() =>
        {
            var positions = Enumerable.Range(1, 3).Select(i => Position($"Security {i}", 1m)).ToList();

            var cut = RenderTable(positions, 3m, maxListEntries: 2);

            var rows = cut.FindAll("tbody tr");
            // 2 capped position rows + 1 "more entries" row + 1 total row.
            Assert.Equal(4, rows.Count);
            Assert.Equal("and 1 more", cut.Find(".kpi-explanation-more").TextContent);
        });
    }

    /// <summary>
    /// When the position count exactly equals <c>MaxListEntries</c>, no "and N more" row is rendered
    /// (off-by-one boundary: the cap is only exceeded, not merely reached).
    /// </summary>
    [Fact]
    public void Render_ExactlyMaxListEntries_NoMoreEntriesRow()
    {
        WithInvariantCulture(() =>
        {
            var positions = Enumerable.Range(1, 2).Select(i => Position($"Security {i}", 1m)).ToList();

            var cut = RenderTable(positions, 2m, maxListEntries: 2);

            var rows = cut.FindAll("tbody tr");
            // 2 position rows + 1 total row, no "more entries" row.
            Assert.Equal(3, rows.Count);
            Assert.Empty(cut.FindAll(".kpi-explanation-more"));
        });
    }

    /// <summary>
    /// With <c>ColorizePositiveNegative = true</c>, positive and negative values get the "positive"/"negative"
    /// CSS class respectively, including on the total row.
    /// </summary>
    [Fact]
    public void Render_ColorizePositiveNegativeTrue_AppliesColorClassToRowsAndTotal()
    {
        WithInvariantCulture(() =>
        {
            var positions = new List<PortfolioTopPosition> { Position("Gain", 10m), Position("Loss", -5m) };

            var cut = RenderTable(positions, 5m, colorizePositiveNegative: true);

            var valueCells = cut.FindAll("tbody tr td:nth-child(2)");
            Assert.Contains("positive", valueCells[0].ClassList);
            Assert.Contains("negative", valueCells[1].ClassList);
            Assert.Contains("positive", valueCells[2].ClassList); // total row: 5m >= 0
        });
    }

    /// <summary>
    /// With <c>ColorizePositiveNegative = false</c> (the default), no "positive"/"negative" CSS class is
    /// applied to any row, including the total row, regardless of sign.
    /// </summary>
    [Fact]
    public void Render_ColorizePositiveNegativeFalse_NoColorClassAnywhere()
    {
        WithInvariantCulture(() =>
        {
            var positions = new List<PortfolioTopPosition> { Position("Gain", 10m), Position("Loss", -5m) };

            var cut = RenderTable(positions, 5m, colorizePositiveNegative: false);

            var valueCells = cut.FindAll("tbody tr td:nth-child(2)");
            Assert.All(valueCells, cell =>
            {
                Assert.DoesNotContain("positive", cell.ClassList);
                Assert.DoesNotContain("negative", cell.ClassList);
            });
        });
    }
}

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
/// Tests for the "total market value" derivation shown in <see cref="PortfolioStructureCard"/>'s
/// KPI explanation panel: the total must equal the sum of the shown top positions plus an
/// "other positions" remainder row for any positions beyond the top 10.
/// </summary>
public sealed class PortfolioStructureCardTests : BunitContext
{
    private static PortfolioStructureDto BuildData(decimal totalMarketValue, IReadOnlyList<PortfolioTopPosition> topPositions)
        => new(totalMarketValue, 0m, totalMarketValue, [], [], [], topPositions);

    /// <summary>
    /// When the top-10 list already covers the full market value, no "other positions" remainder row is rendered.
    /// </summary>
    [Fact]
    public void TotalMarketValueExplanation_TopPositionsCoverTotal_NoOtherPositionsRow()
    {
        WithInvariantCulture(() =>
        {
            Services.AddLocalization(options => options.ResourcesPath = "Resources");
            Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());

            var positions = new List<PortfolioTopPosition>
            {
                new(Guid.NewGuid(), "Security A", 600m, 0.6m, 100m),
                new(Guid.NewGuid(), "Security B", 400m, 0.4m, 50m)
            };
            var data = BuildData(1000m, positions);

            var cut = Render<PortfolioStructureCard>(builder => builder.Add(c => c.Data, data));

            cut.Find(".kpi-info-btn").Click();

            var rows = cut.FindAll(".kpi-explanation-table tbody tr");
            // 2 position rows + 1 total row, no "other positions" row.
            Assert.Equal(3, rows.Count);
            Assert.DoesNotContain(rows, r => r.TextContent.Contains("Other positions", StringComparison.OrdinalIgnoreCase));
        });
    }

    /// <summary>
    /// When more than 10 positions exist, the top-10 list under-covers the total; the explanation must show the
    /// remainder as an "other positions" row so the listed values still sum to the displayed total.
    /// </summary>
    [Fact]
    public void TotalMarketValueExplanation_TopPositionsBelowTotal_AddsOtherPositionsRowWithRemainder()
    {
        WithInvariantCulture(() =>
        {
            Services.AddLocalization(options => options.ResourcesPath = "Resources");
            Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());

            var positions = new List<PortfolioTopPosition>
            {
                new(Guid.NewGuid(), "Security A", 600m, 0.5m, 100m),
                new(Guid.NewGuid(), "Security B", 300m, 0.25m, 50m)
            };
            // Total market value exceeds the sum of the listed top positions (900) by 200 -> remaining positions.
            var data = BuildData(1100m, positions);

            var cut = Render<PortfolioStructureCard>(builder => builder.Add(c => c.Data, data));

            cut.Find(".kpi-info-btn").Click();

            var rows = cut.FindAll(".kpi-explanation-table tbody tr");
            // 2 position rows + 1 "other positions" row + 1 total row.
            Assert.Equal(4, rows.Count);

            var otherRow = rows.Single(r => r.TextContent.Contains("Other positions", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("200", otherRow.TextContent);

            var totalRow = cut.Find(".kpi-explanation-total");
            Assert.Contains("1,100", totalRow.TextContent);
        });
    }
}

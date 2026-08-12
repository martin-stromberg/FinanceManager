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
/// KPI explanation panel: the panel must list all positions (not just the top 10) so the listed
/// values always sum to the displayed total.
/// </summary>
public sealed class PortfolioStructureCardTests : BunitContext
{
    private static PortfolioStructureDto BuildData(
        decimal totalMarketValue,
        IReadOnlyList<PortfolioTopPosition> allPositions,
        IReadOnlyList<PortfolioInvestedCapitalPosition>? investedCapitalBreakdown = null)
        => new(totalMarketValue, 0m, totalMarketValue, [], [], [], allPositions, allPositions, investedCapitalBreakdown ?? []);

    /// <summary>
    /// The total market value explanation lists every position (not only the top 10) plus the total row.
    /// </summary>
    [Fact]
    public void TotalMarketValueExplanation_ListsAllPositions()
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
            // 2 position rows + 1 total row.
            Assert.Equal(3, rows.Count);
        });
    }

    /// <summary>
    /// When more than 10 positions exist, all of them (not just the top 10) are listed in the
    /// total market value explanation, and the list is wrapped in a scrollable container.
    /// </summary>
    [Fact]
    public void TotalMarketValueExplanation_MoreThanTenPositions_ListsAllAndIsScrollable()
    {
        WithInvariantCulture(() =>
        {
            Services.AddLocalization(options => options.ResourcesPath = "Resources");
            Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());

            var positions = Enumerable.Range(1, 15)
                .Select(i => new PortfolioTopPosition(Guid.NewGuid(), $"Security {i}", 100m, 100m / 1500m, 10m))
                .ToList();
            var data = BuildData(1500m, positions);

            var cut = Render<PortfolioStructureCard>(builder => builder.Add(c => c.Data, data));

            cut.Find(".kpi-info-btn").Click();

            var rows = cut.FindAll(".kpi-explanation-table tbody tr");
            // 15 position rows + 1 total row.
            Assert.Equal(16, rows.Count);
            Assert.NotNull(cut.Find(".kpi-explanation-scroll"));
        });
    }

    /// <summary>
    /// When more than <c>MaxListEntries</c> (200) positions exist, the total market value explanation
    /// caps the rendered rows at 200 and adds an "and N more" row for the remainder.
    /// </summary>
    [Fact]
    public void TotalMarketValueExplanation_MoreThanMaxListEntriesPositions_CapsListAndShowsMoreEntriesRow()
    {
        WithInvariantCulture(() =>
        {
            Services.AddLocalization(options => options.ResourcesPath = "Resources");
            Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());

            var positions = Enumerable.Range(1, 201)
                .Select(i => new PortfolioTopPosition(Guid.NewGuid(), $"Security {i}", 1m, 1m / 201m, 0m))
                .ToList();
            var data = BuildData(201m, positions);

            var cut = Render<PortfolioStructureCard>(builder => builder.Add(c => c.Data, data));

            cut.Find(".kpi-info-btn").Click();

            var rows = cut.FindAll(".kpi-explanation-table tbody tr");
            // 200 capped position rows + 1 "more entries" row + 1 total row.
            Assert.Equal(202, rows.Count);

            var moreRow = cut.Find(".kpi-explanation-more");
            Assert.Equal("and 1 more", moreRow.TextContent);
        });
    }

    /// <summary>
    /// When exactly <c>MaxListEntries</c> (200) positions exist, no "and N more" row is rendered
    /// (off-by-one boundary: the cap is only exceeded, not merely reached).
    /// </summary>
    [Fact]
    public void TotalMarketValueExplanation_ExactlyMaxListEntriesPositions_NoMoreEntriesRow()
    {
        WithInvariantCulture(() =>
        {
            Services.AddLocalization(options => options.ResourcesPath = "Resources");
            Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());

            var positions = Enumerable.Range(1, 200)
                .Select(i => new PortfolioTopPosition(Guid.NewGuid(), $"Security {i}", 1m, 1m / 200m, 0m))
                .ToList();
            var data = BuildData(200m, positions);

            var cut = Render<PortfolioStructureCard>(builder => builder.Add(c => c.Data, data));

            cut.Find(".kpi-info-btn").Click();

            var rows = cut.FindAll(".kpi-explanation-table tbody tr");
            // 200 position rows + 1 total row, no "more entries" row.
            Assert.Equal(201, rows.Count);
            Assert.Empty(cut.FindAll(".kpi-explanation-more"));
        });
    }

    /// <summary>
    /// When a security has more than <c>MaxListEntries</c> (200) FIFO lots, the invested capital
    /// accordion caps the rendered lot rows at 200 and adds an "and N more" row for the remainder.
    /// </summary>
    [Fact]
    public void InvestedCapitalExplanation_MoreThanMaxListEntriesLots_CapsListAndShowsMoreEntriesRow()
    {
        WithInvariantCulture(() =>
        {
            Services.AddLocalization(options => options.ResourcesPath = "Resources");
            Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());

            var lots = Enumerable.Range(1, 201)
                .Select(i => new PortfolioInvestedCapitalLot(new DateTime(2024, 1, 1).AddDays(i), 1m, 1m, 1m))
                .ToList();
            var breakdown = new List<PortfolioInvestedCapitalPosition>
            {
                new(Guid.NewGuid(), "Security A", 201m, lots)
            };
            var data = BuildData(1000m, [], breakdown);

            var cut = Render<PortfolioStructureCard>(builder => builder.Add(c => c.Data, data));

            // The "invested capital" info button is the second .kpi-info-btn in the card.
            cut.FindAll(".kpi-info-btn")[1].Click();

            var rows = cut.FindAll(".kpi-invested-capital-accordion .kpi-explanation-table tbody tr");
            // 200 capped lot rows + 1 "more entries" row.
            Assert.Equal(201, rows.Count);

            var moreRow = cut.Find(".kpi-explanation-more");
            Assert.Equal("and 1 more", moreRow.TextContent);
        });
    }

    /// <summary>
    /// When a security has exactly <c>MaxListEntries</c> (200) FIFO lots, no "and N more" row is
    /// rendered in the invested capital accordion (off-by-one boundary).
    /// </summary>
    [Fact]
    public void InvestedCapitalExplanation_ExactlyMaxListEntriesLots_NoMoreEntriesRow()
    {
        WithInvariantCulture(() =>
        {
            Services.AddLocalization(options => options.ResourcesPath = "Resources");
            Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());

            var lots = Enumerable.Range(1, 200)
                .Select(i => new PortfolioInvestedCapitalLot(new DateTime(2024, 1, 1).AddDays(i), 1m, 1m, 1m))
                .ToList();
            var breakdown = new List<PortfolioInvestedCapitalPosition>
            {
                new(Guid.NewGuid(), "Security A", 200m, lots)
            };
            var data = BuildData(1000m, [], breakdown);

            var cut = Render<PortfolioStructureCard>(builder => builder.Add(c => c.Data, data));

            // The "invested capital" info button is the second .kpi-info-btn in the card.
            cut.FindAll(".kpi-info-btn")[1].Click();

            var rows = cut.FindAll(".kpi-invested-capital-accordion .kpi-explanation-table tbody tr");
            Assert.Equal(200, rows.Count);
            Assert.Empty(cut.FindAll(".kpi-explanation-more"));
        });
    }

    /// <summary>
    /// The invested capital explanation renders an accordion entry per security; expanding it
    /// reveals the FIFO lots (purchase postings) that make up that security's invested capital.
    /// </summary>
    [Fact]
    public void InvestedCapitalExplanation_RendersAccordionWithLotsPerSecurity()
    {
        WithInvariantCulture(() =>
        {
            Services.AddLocalization(options => options.ResourcesPath = "Resources");
            Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());

            var breakdown = new List<PortfolioInvestedCapitalPosition>
            {
                new(Guid.NewGuid(), "Security A", 500m,
                [
                    new PortfolioInvestedCapitalLot(new DateTime(2024, 1, 15), 10m, 50m, 500m)
                ])
            };
            var data = BuildData(1000m, [], breakdown);

            var cut = Render<PortfolioStructureCard>(builder => builder.Add(c => c.Data, data));

            // The "invested capital" info button is the second .kpi-info-btn in the card.
            cut.FindAll(".kpi-info-btn")[1].Click();

            var entries = cut.FindAll(".kpi-invested-capital-accordion > li");
            Assert.Single(entries);
            Assert.Contains("Security A", entries[0].TextContent);
            Assert.Contains("500", entries[0].TextContent);
        });
    }
}

using Bunit;
using FinanceManager.Shared.Dtos.Portfolio;
using FinanceManager.Web;
using FinanceManager.Web.Components.Pages.Portfolio;
using FinanceManager.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using static FinanceManager.Tests.TestHelpers.CultureTestHelper;

namespace FinanceManager.Tests.Components.Portfolio;

/// <summary>
/// Tests for <see cref="PortfolioDepotDevelopmentCard"/> — the dashboard tile that renders the
/// year-over-year depot development with its client-side period and yield-basis filters.
/// </summary>
public sealed class PortfolioDepotDevelopmentCardTests : BunitContext
{
    private void RegisterLocalizer()
    {
        Services.AddLocalization(options => options.ResourcesPath = "Resources");
        Services.AddSingleton(typeof(IStringLocalizer<Pages>), new PagesStringLocalizer());
    }

    private static PortfolioDepotDevelopmentYear Year(int year, bool isYtd = false,
        decimal start = 1000m, decimal end = 1100m, decimal dividends = 50m, decimal capital = 800m, decimal netFlow = 800m)
        => new(year, isYtd, start, end, dividends, capital, netFlow);

    /// <summary>
    /// Switching the yield-basis dropdown to invested capital swaps the depot-value table for the
    /// deposits/withdrawals table (four columns) whose yield is calculated on the cumulative net flow.
    /// </summary>
    [Fact]
    public void YieldBasisSwitch_ShowsDepositsTableWithYieldOnNetFlow()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var data = new PortfolioDepotDevelopmentDto([Year(2023, dividends: 50m, netFlow: 800m)]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            var yieldBefore = cut.FindAll("tbody tr td")[5].TextContent;
            cut.Find("select#dev-yield-basis").Change("InvestedCapital");

            var headers = cut.FindAll("thead th");
            Assert.Equal(4, headers.Count);
            Assert.Equal("+5.00%", yieldBefore);

            var cells = cut.FindAll("tbody tr td");
            Assert.Equal("800 €", cells[1].TextContent);
            Assert.Equal("50 €", cells[2].TextContent);
            Assert.Equal("+6.25%", cells[3].TextContent);
        });
    }

    /// <summary>
    /// The cumulative deposits/withdrawals column in invested-capital mode sums the net flows of
    /// all years up to each row, including years outside the visible period filter.
    /// </summary>
    [Fact]
    public void InvestedCapital_CumulativeNetFlow_SpansFullHistory()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var currentYear = DateTime.UtcNow.Year;
            var data = new PortfolioDepotDevelopmentDto(
                Enumerable.Range(currentYear - 2, 3).Select(y => Year(y, netFlow: 100m)).ToList());
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            cut.Find("select#dev-yield-basis").Change("InvestedCapital");

            var rows = cut.FindAll("tbody tr");
            Assert.Equal("100 €", rows[0].QuerySelectorAll("td")[1].TextContent);
            Assert.Equal("200 €", rows[1].QuerySelectorAll("td")[1].TextContent);
            Assert.Equal("300 €", rows[2].QuerySelectorAll("td")[1].TextContent);
        });
    }

    /// <summary>
    /// In invested-capital mode the grouped bar chart (deposits/withdrawals + dividends per year)
    /// replaces the combined bar/line chart.
    /// </summary>
    [Fact]
    public void InvestedCapital_RendersGroupedBarChart()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var data = new PortfolioDepotDevelopmentDto([Year(2023)]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            Assert.Single(cut.FindAll("svg.portfolio-bar-line-chart-svg"));
            Assert.Empty(cut.FindAll("svg.portfolio-grouped-bar-chart-svg"));

            cut.Find("select#dev-yield-basis").Change("InvestedCapital");

            Assert.Empty(cut.FindAll("svg.portfolio-bar-line-chart-svg"));
            Assert.Single(cut.FindAll("svg.portfolio-grouped-bar-chart-svg"));
            Assert.Equal(2, cut.FindAll("rect.portfolio-grouped-bar-chart-bar").Count);
        });
    }

    /// <summary>
    /// The period filter limits the rendered rows to the selected number of trailing years
    /// anchored to the last year of the data series.
    /// </summary>
    [Fact]
    public void PeriodFilter_LimitsRenderedRows()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var currentYear = DateTime.UtcNow.Year;
            var data = new PortfolioDepotDevelopmentDto(
                Enumerable.Range(currentYear - 6, 7).Select(y => Year(y)).ToList());
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            Assert.Equal(7, cut.FindAll("tbody tr").Count);

            cut.Find("select#dev-period-filter").Change("Last5Years");

            Assert.Equal(5, cut.FindAll("tbody tr").Count);
        });
    }

    /// <summary>
    /// The "last N years" period filter is anchored to the last year of the data series, not the
    /// current calendar year — a depot history that ended years ago (e.g. after a complete sale)
    /// still shows its final years instead of an empty result.
    /// </summary>
    [Fact]
    public void PeriodFilter_AnchoredAtLastSeriesYear()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var data = new PortfolioDepotDevelopmentDto(
                Enumerable.Range(2015, 6).Select(y => Year(y)).ToList());
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            cut.Find("select#dev-period-filter").Change("Last5Years");

            var rows = cut.FindAll("tbody tr");
            Assert.Equal(5, rows.Count);
            Assert.Equal("2016", rows[0].QuerySelector("td")!.TextContent);
            Assert.Equal("2020", rows[^1].QuerySelector("td")!.TextContent);
        });
    }

    /// <summary>
    /// A year flagged as YTD renders an asterisk marker in the table and chart label, and the
    /// explanatory footnote below the table mentions YTD.
    /// </summary>
    [Fact]
    public void YtdRow_RendersAsteriskAndFootnote()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var currentYear = DateTime.UtcNow.Year;
            var data = new PortfolioDepotDevelopmentDto(
            [
                Year(currentYear - 1),
                Year(currentYear, isYtd: true)
            ]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            var rows = cut.FindAll("tbody tr");
            Assert.Equal($"{currentYear}*", rows[1].QuerySelector("td")!.TextContent);
            Assert.Contains("YTD", cut.Find(".portfolio-chart-footnote").TextContent);
        });
    }

    /// <summary>
    /// Without any yearly rows the card renders its localized empty-state text instead of
    /// the chart and table.
    /// </summary>
    [Fact]
    public void EmptyYears_RendersEmptyState()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var data = new PortfolioDepotDevelopmentDto([]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            Assert.Equal("No security postings available yet.", cut.Find(".mini-bar-chart-empty").TextContent);
            Assert.Empty(cut.FindAll("tbody tr"));
        });
    }

    /// <summary>
    /// When the selected yield basis is zero the change-percent and yield cells render a dash
    /// instead of dividing by zero.
    /// </summary>
    [Fact]
    public void ZeroBasis_RendersDashInsteadOfYield()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var data = new PortfolioDepotDevelopmentDto([Year(2023, start: 0m, capital: 0m)]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            var cells = cut.FindAll("tbody tr td");
            Assert.Equal("—", cells[3].TextContent);
            Assert.Equal("—", cells[5].TextContent);
        });
    }

    /// <summary>
    /// Nullable cells that render a dash (basis zero, value not computable) must not carry the
    /// negative styling class — a non-computable value is neutral, not a loss.
    /// </summary>
    [Fact]
    public void ZeroBasis_DashCells_RenderWithoutNegativeClass()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var data = new PortfolioDepotDevelopmentDto([Year(2023, start: 0m, capital: 0m)]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            var cells = cut.FindAll("tbody tr td");
            Assert.Equal("—", cells[3].TextContent);
            Assert.Equal("—", cells[5].TextContent);
            Assert.DoesNotContain("negative", cells[3].GetAttribute("class") ?? string.Empty);
            Assert.DoesNotContain("negative", cells[5].GetAttribute("class") ?? string.Empty);
        });
    }

    /// <summary>
    /// In invested-capital mode a zero cumulative net flow renders a dash for the yield instead
    /// of dividing by zero.
    /// </summary>
    [Fact]
    public void ZeroNetFlow_InvestedCapital_RendersDashInsteadOfYield()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var data = new PortfolioDepotDevelopmentDto([Year(2023, netFlow: 0m)]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            cut.Find("select#dev-yield-basis").Change("InvestedCapital");

            var cells = cut.FindAll("tbody tr td");
            Assert.Equal("—", cells[3].TextContent);
            Assert.DoesNotContain("negative", cells[3].GetAttribute("class") ?? string.Empty);
        });
    }

    /// <summary>
    /// Monetary values are rendered culture-aware as rounded euro amounts and the derived
    /// change and yield columns use signed percent formatting.
    /// </summary>
    [Fact]
    public void Formatting_CurrencyAndSignedPercent()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var data = new PortfolioDepotDevelopmentDto([Year(2023)]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            var cells = cut.FindAll("tbody tr td");
            Assert.Equal("2023", cells[0].TextContent);
            // Cells 1 and 2 carry a second value in a mobile-only span that CSS reveals on
            // narrow screens to merge dividend/depot value and change €/% into single columns.
            Assert.Equal("50 € / 1,100 €", cells[1].TextContent);
            Assert.Equal("+100 € / +10.00%", cells[2].TextContent);
            Assert.Equal("+10.00%", cells[3].TextContent);
            Assert.Equal("50 €", cells[4].TextContent);
            Assert.Equal("+5.00%", cells[5].TextContent);
        });
    }

    /// <summary>
    /// The mobile-compact layout marks merged-value cells and hideable columns via dedicated
    /// classes so CSS can collapse the table to four columns without horizontal scrolling.
    /// </summary>
    [Fact]
    public void CompactLayout_CellsCarryMobileClasses()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var data = new PortfolioDepotDevelopmentDto([Year(2023)]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            Assert.Equal(6, cut.FindAll("thead th").Count);
            Assert.Equal(2, cut.FindAll("th.dev-col-changepct, th.dev-col-dividend").Count);
            Assert.NotEmpty(cut.FindAll("td .dev-mobile-only"));
            Assert.Equal(2, cut.FindAll("th .dev-mobile-only").Count);
            Assert.Equal(2, cut.FindAll("th .dev-desktop-only").Count);
        });
    }

    /// <summary>
    /// A yield-basis choice stored in local storage is restored after the first interactive
    /// render: the card switches to the invested-capital view without user input.
    /// </summary>
    [Fact]
    public void StoredYieldBasis_IsRestoredAfterFirstRender()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var js = new FakeJSRuntime();
            js.Store["fm.portfolio.dev.yieldBasis"] = "InvestedCapital";
            Services.AddSingleton<IJSRuntime>(js);

            var data = new PortfolioDepotDevelopmentDto([Year(2023)]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            cut.WaitForAssertion(
                () => Assert.Single(cut.FindAll("svg.portfolio-grouped-bar-chart-svg")),
                timeout: TimeSpan.FromSeconds(5));
        });
    }

    /// <summary>
    /// A period-filter choice stored in local storage is restored after the first interactive
    /// render: the card starts with the filtered row count.
    /// </summary>
    [Fact]
    public void StoredPeriodFilter_IsRestoredAfterFirstRender()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var js = new FakeJSRuntime();
            js.Store["fm.portfolio.dev.period"] = "Last5Years";
            Services.AddSingleton<IJSRuntime>(js);

            var currentYear = DateTime.UtcNow.Year;
            var data = new PortfolioDepotDevelopmentDto(
                Enumerable.Range(currentYear - 6, 7).Select(y => Year(y)).ToList());
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll("tbody tr").Count),
                timeout: TimeSpan.FromSeconds(5));
        });
    }

    /// <summary>
    /// Changing the yield-basis or period dropdown persists the choice to local storage so the
    /// next visit restores it.
    /// </summary>
    [Fact]
    public void FilterChanges_ArePersistedToLocalStorage()
    {
        WithInvariantCulture(() =>
        {
            RegisterLocalizer();
            var js = new FakeJSRuntime();
            Services.AddSingleton<IJSRuntime>(js);

            var data = new PortfolioDepotDevelopmentDto([Year(2023)]);
            var cut = Render<PortfolioDepotDevelopmentCard>(builder => builder.Add(c => c.Data, data));

            cut.Find("select#dev-yield-basis").Change("InvestedCapital");
            cut.Find("select#dev-period-filter").Change("Last10Years");

            cut.WaitForAssertion(() =>
            {
                Assert.Equal("InvestedCapital", js.Store["fm.portfolio.dev.yieldBasis"]);
                Assert.Equal("Last10Years", js.Store["fm.portfolio.dev.period"]);
            }, timeout: TimeSpan.FromSeconds(5));
        });
    }

    /// <summary>Minimal in-memory <see cref="IJSRuntime"/> that answers localStorage calls.</summary>
    private sealed class FakeJSRuntime : IJSRuntime
    {
        public Dictionary<string, string> Store { get; } = new();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "localStorage.getItem" && args?.FirstOrDefault() is string key)
            {
                Store.TryGetValue(key, out var value);
                return new ValueTask<TValue>((TValue)(object?)value!);
            }

            if (identifier == "localStorage.setItem" && args?.ElementAtOrDefault(0) is string setKey)
            {
                Store[setKey] = args.ElementAtOrDefault(1) as string ?? string.Empty;
            }

            return new ValueTask<TValue>(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
    }
}

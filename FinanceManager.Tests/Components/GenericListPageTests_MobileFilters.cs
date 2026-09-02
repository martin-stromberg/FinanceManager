using Bunit;
using FinanceManager.Web.Components.Pages;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Tests.Components;

/// <summary>
/// Tests for <see cref="GenericListPage{T}"/> covering its filter bar (search input and date-range
/// filter, rendered only when the provider opts in) and its mobile-specific row rendering, which
/// lets a provider supply specialized compact card layouts instead of falling back to the generic
/// mobile card presentation.
/// </summary>
public sealed class GenericListPageTests_MobileFilters : BunitContext
{
    /// <summary>
    /// Verifies that with a provider allowing both search and range filtering, the list renders a
    /// search input and a date-range filter with exactly two date inputs and their two labels -
    /// the filter bar's markup actually reflects what the provider says it supports.
    /// </summary>
    [Fact]
    public void Filters_RenderResponsiveSearchAndRangeClasses()
    {
        var provider = new TestListProvider();

        RenderFragment fragment = builder =>
        {
            builder.OpenComponent(0, typeof(GenericListPage<object>));
            builder.AddAttribute(1, "Provider", provider);
            builder.AddAttribute(2, "ShowSearch", true);
            builder.AddAttribute(3, "ShowRange", true);
            builder.CloseComponent();
        };

        var cut = Render(fragment);

        Assert.NotNull(cut.Find(".list-filters"));
        Assert.NotNull(cut.Find("input.list-filter-search"));
        Assert.NotNull(cut.Find(".list-filter-range"));
        Assert.Equal(2, cut.FindAll(".list-filter-range input[type='date']").Count);
        Assert.Equal(2, cut.FindAll(".list-filter-range .list-filter-range-label").Count);
    }

    /// <summary>
    /// Verifies that when a record's <c>MobileRows</c> supplies a specialized two-column layout
    /// (as used for statement-draft date/amount rows), the mobile card renders that layout - with
    /// its muted styling and column labels - instead of the generic column-by-column mobile card,
    /// confirming providers can override the default mobile presentation per record.
    /// </summary>
    [Fact]
    public void MobileCards_RenderSpecializedRows_WhenProvided()
    {
        var provider = new MobileRowsProvider();

        RenderFragment fragment = builder =>
        {
            builder.OpenComponent(0, typeof(GenericListPage<object>));
            builder.AddAttribute(1, "Provider", provider);
            builder.CloseComponent();
        };

        var cut = Render(fragment);

        var card = cut.Find(".generic-list-mobile-card");
        Assert.Contains("muted-row", card.ClassList);
        Assert.Single(cut.FindAll(".generic-list-mobile-row.two-column.statement-draft-entry-date-amount"));
        Assert.Contains("Date", cut.Markup);
        Assert.Contains("Amount", cut.Markup);
    }

    private sealed class TestListProvider : IListProvider
    {
        public IReadOnlyList<object> Items { get; } = new List<object> { new() };
        public bool CanLoadMore => false;
        public bool Loading => false;
        public string Search { get; private set; } = string.Empty;
        public DateTime? RangeFrom { get; private set; }
        public DateTime? RangeTo { get; private set; }
        public IReadOnlyList<ListColumn> Columns { get; } = new[] { new ListColumn("name", "Name") };
        public IReadOnlyList<ListRecord> Records { get; } = new[] { new ListRecord(new[] { new ListCell(ListCellKind.Text, "Item") }, new object()) };
        public bool AllowRangeFiltering => true;
        public bool AllowSearchFiltering => true;
#pragma warning disable CS0067 // test double never raises this event; declared only to satisfy IListProvider
        public event EventHandler? StateChanged;
#pragma warning restore CS0067

        public Task InitializeAsync() => Task.CompletedTask;
        public Task LoadAsync() => Task.CompletedTask;
        public Task LoadMoreAsync() => Task.CompletedTask;
        public void ClearSearch() => Search = string.Empty;
        public void ClearRange() { RangeFrom = null; RangeTo = null; }
        public void SetSearch(string value) => Search = value;
        public void SetRange(DateTime? from, DateTime? to) { RangeFrom = from; RangeTo = to; }
        public void ResetAndSearch() { }
        public IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer) => null;
    }

    private sealed class MobileRowsProvider : IListProvider
    {
        public IReadOnlyList<object> Items { get; } = new List<object> { new() };
        public bool CanLoadMore => false;
        public bool Loading => false;
        public string Search { get; private set; } = string.Empty;
        public DateTime? RangeFrom { get; private set; }
        public DateTime? RangeTo { get; private set; }
        public IReadOnlyList<ListColumn> Columns { get; } = new[] { new ListColumn("date", "Date"), new ListColumn("amount", "Amount") };
        public IReadOnlyList<ListRecord> Records { get; } = new[]
        {
            new ListRecord(
                new[]
                {
                    new ListCell(ListCellKind.Text, "7/1/2026", Muted: true),
                    new ListCell(ListCellKind.Currency, Amount: 12.34m, Muted: true)
                },
                new object(),
                MobileRows: new[]
                {
                    new ListMobileRow(
                        new[]
                        {
                            new ListMobileCell("Date", new ListCell(ListCellKind.Text, "7/1/2026", Muted: true)),
                            new ListMobileCell("Amount", new ListCell(ListCellKind.Currency, Amount: 12.34m, Muted: true))
                        },
                        ListMobileRowKind.TwoColumn,
                        "statement-draft-entry-date-amount")
                })
        };
        public bool AllowRangeFiltering => true;
        public bool AllowSearchFiltering => true;
#pragma warning disable CS0067 // test double never raises this event; declared only to satisfy IListProvider
        public event EventHandler? StateChanged;
#pragma warning restore CS0067

        public Task InitializeAsync() => Task.CompletedTask;
        public Task LoadAsync() => Task.CompletedTask;
        public Task LoadMoreAsync() => Task.CompletedTask;
        public void ClearSearch() => Search = string.Empty;
        public void ClearRange() { RangeFrom = null; RangeTo = null; }
        public void SetSearch(string value) => Search = value;
        public void SetRange(DateTime? from, DateTime? to) { RangeFrom = from; RangeTo = to; }
        public void ResetAndSearch() { }
        public IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer) => null;
    }
}

using Bunit;
using FinanceManager.Web.Components.Pages;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Tests.Components;

public sealed class GenericListPageTests_MobileFilters : BunitContext
{
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
        public event EventHandler? StateChanged;

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

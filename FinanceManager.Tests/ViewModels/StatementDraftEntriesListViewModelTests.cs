using FinanceManager.Web;
using FinanceManager.Web.ViewModels.StatementDrafts;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers the posting-type display helper and the navigation URL used by the statement-draft
/// entries list in quick-edit mode: <see cref="BankPostingType.Unspecified"/> renders empty,
/// defined values render the localized resource text, and every persisted entry links to its
/// entry card.
/// </summary>
public sealed class StatementDraftEntriesListViewModelTests
{
    /// <summary>Localizer backed by an explicit key/value map; unknown keys report ResourceNotFound.</summary>
    private sealed class MapLocalizer<T> : IStringLocalizer<T>
    {
        private readonly IReadOnlyDictionary<string, string> _map;
        public MapLocalizer(IReadOnlyDictionary<string, string> map) => _map = map;
        public LocalizedString this[string name]
            => _map.TryGetValue(name, out var v)
                ? new LocalizedString(name, v)
                : new LocalizedString(name, name, resourceNotFound: true);
        public LocalizedString this[string name, params object[] arguments] => this[name];
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => _map.Select(kv => new LocalizedString(kv.Key, kv.Value));
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    /// <summary><see cref="BankPostingType.Unspecified"/> must render as an empty cell.</summary>
    [Fact]
    public void BuildPostingTypeText_Unspecified_ReturnsEmpty()
    {
        var loc = new MapLocalizer<Pages>(new Dictionary<string, string>
        {
            ["EnumType_BankPostingType_Tax"] = "Steuer"
        });

        Assert.Equal(string.Empty, StatementDraftEntriesListViewModel.BuildPostingTypeText(BankPostingType.Unspecified, loc));
    }

    /// <summary>A defined posting type must render the localized resource text.</summary>
    [Fact]
    public void BuildPostingTypeText_UsesLocalizedResource()
    {
        var loc = new MapLocalizer<Pages>(new Dictionary<string, string>
        {
            ["EnumType_BankPostingType_Tax"] = "Steuer",
            ["EnumType_BankPostingType_Interest"] = "Zinsen"
        });

        Assert.Equal("Steuer", StatementDraftEntriesListViewModel.BuildPostingTypeText(BankPostingType.Tax, loc));
        Assert.Equal("Zinsen", StatementDraftEntriesListViewModel.BuildPostingTypeText(BankPostingType.Interest, loc));
    }

    /// <summary>Without a matching resource the enum name is the fallback.</summary>
    [Fact]
    public void BuildPostingTypeText_FallsBackToEnumName()
    {
        var loc = new MapLocalizer<Pages>(new Dictionary<string, string>());
        Assert.Equal("Tax", StatementDraftEntriesListViewModel.BuildPostingTypeText(BankPostingType.Tax, loc));
    }

    /// <summary>The navigation URL of an entry item must point to its entry card.</summary>
    [Fact]
    public void EntryItem_NavigateUrl_PointsToEntryCard()
    {
        var entryId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var item = new StatementDraftEntryItem { Id = entryId, DraftId = draftId };

        Assert.Equal($"/card/statement-drafts/entries/{entryId}?draftId={draftId}", item.GetNavigateUrl());
    }
}

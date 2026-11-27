using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Web.Services;
using FinanceManager.Web.ViewModels.Accounts;
using FinanceManager.Web.ViewModels.Budget;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.Securities;
using FinanceManager.Web.ViewModels.Setup;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Verifies the target ribbon configuration (group titles, order via <see cref="UiRibbonTab.Sort"/>,
/// <see cref="UiRibbonItemSize"/> per action, conditional "Navigieren" group generation) of
/// representative view models after the ribbon rework — covering a list view with conditional back
/// context (<see cref="BankAccountListViewModel"/>), a report view (<see cref="BudgetReportViewModel"/>),
/// a grouped list view (<see cref="SecuritiesListViewModel"/>), a detail card
/// (<see cref="SecurityCardViewModel"/>) and the composite setup card with its section view models.
/// </summary>
public sealed class RibbonConfigTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager(string baseUri = "http://localhost/")
        {
            Initialize(baseUri, baseUri);
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            // no-op for tests
        }
    }

    private sealed class PassthroughLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }

    private sealed class PassthroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }

    // Builds a minimal service provider containing every dependency the view models under test
    // resolve (current user, API client, navigation manager, generic localizer, KPI cache, logging).
    private static (IServiceProvider sp, Mock<IApiClient> apiMock) BuildServices(bool isAdmin = false)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService { IsAuthenticated = true, IsAdmin = isAdmin });
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        services.AddSingleton<NavigationManager>(new TestNavigationManager());
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassthroughLocalizer<>));
        services.AddSingleton<IKpiLocalStorageCache>(Mock.Of<IKpiLocalStorageCache>());
        services.AddLogging();
        return (services.BuildServiceProvider(), apiMock);
    }

    private static List<UiRibbonTab> GetTabs(BaseViewModel vm)
        => (vm.GetRibbonRegisters(new PassthroughLocalizer()) ?? new List<UiRibbonRegister>())
            .SelectMany(r => r.Tabs ?? new List<UiRibbonTab>())
            .OrderBy(t => t.Sort)
            .ToList();

    // ------------------------------------------------------------------
    // BankAccountListViewModel — conditional "Navigieren" group
    // ------------------------------------------------------------------

    /// <summary>
    /// Without a back context (<c>SetBackNavigation</c> never called) the account list must not
    /// emit a "Navigieren" tab at all — the conditional group generation replaces the previous
    /// hidden-button approach so no empty group frame can appear.
    /// </summary>
    [Fact]
    public void BankAccountList_WithoutBackContext_EmitsNoNavigationTab()
    {
        var (sp, _) = BuildServices();
        var vm = new BankAccountListViewModel(sp);

        var tabs = GetTabs(vm);

        Assert.DoesNotContain(tabs, t => t.Title == "Ribbon_Group_Navigation");
        Assert.Equal(2, tabs.Count);
        Assert.Equal("Ribbon_Group_Manage", tabs[0].Title);
        Assert.Equal(10, tabs[0].Sort);
        Assert.Equal("Ribbon_Group_Filter", tabs[1].Title);
        Assert.Equal(30, tabs[1].Sort);
    }

    /// <summary>
    /// After <c>SetBackNavigation</c> supplies a URL the "Navigieren" tab is generated at Sort 0
    /// with a large "Back" action; clearing the context with <c>null</c> removes the tab again.
    /// </summary>
    [Fact]
    public void BankAccountList_BackContext_TogglesNavigationTab()
    {
        var (sp, _) = BuildServices();
        var vm = new BankAccountListViewModel(sp);
        var loc = new PassthroughLocalizer();

        vm.SetBackNavigation("/list/contacts");
        var tabs = GetTabs(vm);
        var navTab = Assert.Single(tabs, t => t.Title == "Ribbon_Group_Navigation");
        Assert.Equal(0, navTab.Sort);
        var back = Assert.Single(navTab.Items);
        Assert.Equal("Back", back.Id);
        Assert.Equal(UiRibbonItemSize.Large, back.Size);
        Assert.True(back.MobileShortcut);
        Assert.Equal("Ribbon_Group_Navigation", tabs[0].Title);

        vm.SetBackNavigation(null);
        tabs = GetTabs(vm);
        Assert.DoesNotContain(tabs, t => t.Title == "Ribbon_Group_Navigation");
    }

    /// <summary>
    /// The account list's target sizes: "New" large in "Verwalten", "ClearSearch" large in "Filter"
    /// and disabled while no search term is set.
    /// </summary>
    [Fact]
    public void BankAccountList_ManageAndFilter_SizesAndDisabledState()
    {
        var (sp, _) = BuildServices();
        var vm = new BankAccountListViewModel(sp);

        var tabs = GetTabs(vm);
        var manage = Assert.Single(tabs, t => t.Title == "Ribbon_Group_Manage");
        var newAction = Assert.Single(manage.Items);
        Assert.Equal("New", newAction.Id);
        Assert.Equal(UiRibbonItemSize.Large, newAction.Size);
        Assert.True(newAction.MobileShortcut);

        var filter = Assert.Single(tabs, t => t.Title == "Ribbon_Group_Filter");
        var clearSearch = Assert.Single(filter.Items);
        Assert.Equal("ClearSearch", clearSearch.Id);
        Assert.Equal(UiRibbonItemSize.Large, clearSearch.Size);
        Assert.True(clearSearch.Disabled); // no search term set
    }

    // ------------------------------------------------------------------
    // BudgetReportViewModel — conditional "Navigieren" group + sizes
    // ------------------------------------------------------------------

    /// <summary>
    /// The budget report emits no "Navigieren" tab when opened directly (no back context),
    /// keeping only Verwalten/Analyse/Export.
    /// </summary>
    [Fact]
    public void BudgetReport_WithoutBackContext_EmitsNoNavigationTab()
    {
        var (sp, _) = BuildServices();
        var vm = new BudgetReportViewModel(sp);

        var tabs = GetTabs(vm);

        Assert.DoesNotContain(tabs, t => t.Title == "Ribbon_Group_Navigation");
        Assert.Equal(new[] { "Ribbon_Group_Manage", "Ribbon_Group_Analysis", "Ribbon_Group_Export" }, tabs.Select(t => t.Title));
    }

    /// <summary>
    /// With a back context the "Navigieren" tab appears first (Sort 0) containing a large "Back"
    /// action — required for the back chain from the budget purpose list.
    /// </summary>
    [Fact]
    public void BudgetReport_WithBackContext_EmitsNavigationTabFirst()
    {
        var (sp, _) = BuildServices();
        var vm = new BudgetReportViewModel(sp);

        vm.SetBackNavigation("list/budget");
        var tabs = GetTabs(vm);

        Assert.Equal("Ribbon_Group_Navigation", tabs[0].Title);
        Assert.Equal(0, tabs[0].Sort);
        var back = Assert.Single(tabs[0].Items);
        Assert.Equal("Back", back.Id);
        Assert.Equal(UiRibbonItemSize.Large, back.Size);
    }

    /// <summary>
    /// Target sizes on the budget report: "Settings" promoted to large in "Verwalten", all
    /// quick-range actions small in "Analyse", "ExportExcel" small in "Export".
    /// </summary>
    [Fact]
    public void BudgetReport_ActionSizes_MatchTargetConfiguration()
    {
        var (sp, _) = BuildServices();
        var vm = new BudgetReportViewModel(sp);

        var tabs = GetTabs(vm);
        var manage = Assert.Single(tabs, t => t.Title == "Ribbon_Group_Manage");
        var settings = Assert.Single(manage.Items);
        Assert.Equal("Settings", settings.Id);
        Assert.Equal(UiRibbonItemSize.Large, settings.Size);

        var analysis = Assert.Single(tabs, t => t.Title == "Ribbon_Group_Analysis");
        Assert.Equal(new[] { "PrevMonth", "ThisMonth", "NextMonth", "PrevYear", "NextYear" }, analysis.Items.Select(a => a.Id));
        Assert.All(analysis.Items, a => Assert.Equal(UiRibbonItemSize.Small, a.Size));

        var export = Assert.Single(tabs, t => t.Title == "Ribbon_Group_Export");
        var exportExcel = Assert.Single(export.Items);
        Assert.Equal("ExportExcel", exportExcel.Id);
        Assert.Equal(UiRibbonItemSize.Small, exportExcel.Size);
    }

    // ------------------------------------------------------------------
    // SecuritiesListViewModel — Manage/View/Reports groups
    // ------------------------------------------------------------------

    /// <summary>
    /// The securities list splits into "Verwalten" (New, Categories — both large), "Ansicht"
    /// (ToggleActive small) and "Berichte" (PortfolioAnalysisReport large) in sort order 10/30/70.
    /// </summary>
    [Fact]
    public void SecuritiesList_Groups_ManageViewReports_InOrder()
    {
        var (sp, _) = BuildServices();
        var vm = new SecuritiesListViewModel(sp);

        var tabs = GetTabs(vm);

        Assert.Equal(new[] { "Ribbon_Group_Manage", "Ribbon_Group_View", "Ribbon_Group_Reports" }, tabs.Select(t => t.Title));
        Assert.Equal(new[] { 10, 30, 70 }, tabs.Select(t => t.Sort));

        var manage = tabs[0];
        Assert.Equal(new[] { "New", "Categories" }, manage.Items.Select(a => a.Id));
        Assert.All(manage.Items, a => Assert.Equal(UiRibbonItemSize.Large, a.Size));

        var view = tabs[1];
        var toggleActive = Assert.Single(view.Items);
        Assert.Equal("ToggleActive", toggleActive.Id);
        Assert.Equal(UiRibbonItemSize.Small, toggleActive.Size);

        var reports = tabs[2];
        var report = Assert.Single(reports.Items);
        Assert.Equal("PortfolioAnalysisReport", report.Id);
        Assert.Equal(UiRibbonItemSize.Large, report.Size);
    }

    // ------------------------------------------------------------------
    // SecurityCardViewModel — Linked without Performance, Reports group
    // ------------------------------------------------------------------

    /// <summary>
    /// The security card keeps linked-information actions (Postings, Attachments, Prices — all
    /// small) in their own group while "Performance" was promoted into a separate "Berichte"
    /// group rendered as a large action.
    /// </summary>
    [Fact]
    public void SecurityCard_LinkedWithoutPerformance_AndReportsLarge()
    {
        var (sp, _) = BuildServices();
        var vm = new SecurityCardViewModel(sp);

        var tabs = GetTabs(vm);

        Assert.Equal(
            new[] { "Ribbon_Group_Navigation", "Ribbon_Group_Manage", "Ribbon_Group_Linked", "Ribbon_Group_Reports" },
            tabs.Select(t => t.Title));

        var linked = tabs.Single(t => t.Title == "Ribbon_Group_Linked");
        Assert.Equal(new[] { "Postings", "Attachments", "Prices" }, linked.Items.Select(a => a.Id));
        Assert.DoesNotContain(linked.Items, a => a.Id == "Performance");
        Assert.All(linked.Items, a => Assert.Equal(UiRibbonItemSize.Small, a.Size));

        var reports = tabs.Single(t => t.Title == "Ribbon_Group_Reports");
        var performance = Assert.Single(reports.Items);
        Assert.Equal("Performance", performance.Id);
        Assert.Equal(UiRibbonItemSize.Large, performance.Size);
    }

    // ------------------------------------------------------------------
    // SetupCardViewModel — sort order across own + section view models
    // ------------------------------------------------------------------

    /// <summary>
    /// For an admin the aggregated setup ribbon orders its groups per the sort convention:
    /// Verwalten(10) → Daten(15) → Profil(20) → KI(35) → Programmversion(50) → Sicherungen(MaxValue).
    /// </summary>
    [Fact]
    public async Task SetupCard_AdminRibbonGroups_FollowSortConvention()
    {
        var (sp, apiMock) = BuildServices(isAdmin: true);
        apiMock.Setup(a => a.Mcp_GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new McpStatusDto(true));
        apiMock.Setup(a => a.Mcp_GetPendingApprovalCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var vm = new SetupCardViewModel(sp);
        await vm.LoadAsync(Guid.Empty);

        var tabs = GetTabs(vm);

        var orderedTitles = tabs.Select(t => t.Title).ToList();
        Assert.Equal(new[]
        {
            "Ribbon_Group_Manage",
            "Ribbon_Group_Data",
            "SetupProfile_Title",
            "Ribbon_Group_Mcp",
            "Setup_Section_Update",
            "SetupBackup_Titel"
        }, orderedTitles);
    }
}

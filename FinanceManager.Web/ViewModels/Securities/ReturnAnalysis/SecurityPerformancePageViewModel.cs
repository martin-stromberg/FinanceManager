using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Securities.ReturnAnalysis;

/// <summary>
/// View model for the security performance page shell.
/// </summary>
public sealed class SecurityPerformancePageViewModel : BaseViewModel
{
    private static readonly string[] TabKeys = ["Overview", "TimeSeries", "Cashflows", "Metrics", "Benchmark"];

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="services">Service provider.</param>
    public SecurityPerformancePageViewModel(IServiceProvider services) : base(services)
    {
        OverviewTabVm = CreateSubViewModel<SecurityPerformanceOverviewTabViewModel>(singletonPerType: true);
    }

    /// <summary>
    /// View model for the overview tab. Owned and lifecycle-managed by this page view model
    /// so that its state (selected range, chart data) can be accessed for ribbon button rendering.
    /// </summary>
    public SecurityPerformanceOverviewTabViewModel OverviewTabVm { get; }

    /// <summary>
    /// Security identifier currently shown by the page.
    /// </summary>
    public Guid SecurityId { get; private set; }

    /// <summary>
    /// Name of the selected security.
    /// </summary>
    public string? SecurityName { get; private set; }

    /// <summary>
    /// Gets whether the requested security could not be found.
    /// </summary>
    public bool NotFound { get; private set; }

    /// <summary>
    /// Active tab key.
    /// </summary>
    public string ActiveTabKey { get; private set; } = TabKeys[0];

    /// <summary>
    /// All available tab keys.
    /// </summary>
    public IReadOnlyList<string> Tabs => TabKeys;

    /// <summary>
    /// Loads page header data for the given security.
    /// </summary>
    /// <param name="securityId">Security identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task LoadAsync(Guid securityId, CancellationToken ct = default)
    {
        if (!CheckAuthentication())
        {
            return;
        }

        SecurityId = securityId;
        NotFound = false;
        SecurityName = null;

        try
        {
            var security = await ApiClient.Securities_GetAsync(securityId, ct);
            if (security == null)
            {
                NotFound = true;
                RaiseStateChanged();
                return;
            }

            SecurityName = security.Name;
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode ?? string.Empty, ApiClient.LastError ?? ex.Message);
            NotFound = true;
        }

        RaiseStateChanged();
    }

    /// <summary>
    /// Sets the active tab key if valid.
    /// </summary>
    /// <param name="tabKey">Target tab key.</param>
    public void SelectTab(string tabKey)
    {
        if (!TabKeys.Contains(tabKey, StringComparer.Ordinal))
        {
            return;
        }

        ActiveTabKey = tabKey;
        RaiseStateChanged();
    }

    /// <summary>
    /// Returns a localized display label for a tab key.
    /// </summary>
    /// <param name="tabKey">Tab key.</param>
    /// <returns>Display label.</returns>
    public static string GetTabLabel(string tabKey) => tabKey switch
    {
        "Overview" => "Übersicht",
        "TimeSeries" => "Zeitliche Entwicklung",
        "Cashflows" => "Cashflows",
        "Metrics" => "Kennzahlen",
        "Benchmark" => "Benchmark",
        _ => tabKey
    };

    /// <inheritdoc />
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var tabs = new List<UiRibbonTab>();

        // Navigation group: always-visible back button to the security card.
        tabs.Add(new UiRibbonTab(
            localizer["Ribbon_Group_Navigation"].Value,
            new List<UiRibbonAction>
            {
                new UiRibbonAction(
                    "Back",
                    localizer["Ribbon_Back"].Value,
                    "<svg><use href='/icons/sprite.svg#back'/></svg>",
                    UiRibbonItemSize.Large,
                    false,
                    null,
                    () => { Navigation.NavigateTo($"/card/securities/{SecurityId}"); return Task.CompletedTask; }
                )
            },
            Sort: 0
        ));

        // Zeitraum group: time range selector, only shown while the Overview tab is active.
        if (ActiveTabKey == "Overview")
        {
            tabs.Add(new UiRibbonTab(
                localizer["Ribbon_Group_Period"].Value,
                OverviewTabVm.Ranges
                    .Select(range => new UiRibbonAction(
                        $"Range_{range}",
                        (range == OverviewTabVm.SelectedRange ? "▸ " : string.Empty) + SecurityPerformanceOverviewTabViewModel.GetRangeLabel(range),
                        "<svg><use href='/icons/sprite.svg#calendar'/></svg>",
                        UiRibbonItemSize.Small,
                        false,
                        null,
                        async () => { await OverviewTabVm.SelectRangeAsync(range); }
                    ))
                    .ToList(),
                Sort: 10
            ));
        }

        // Einstellungen group: benchmark setup button, only shown while the Benchmark tab is active.
        if (ActiveTabKey == "Benchmark")
        {
            tabs.Add(new UiRibbonTab(
                localizer["Ribbon_Group_Settings"].Value,
                new List<UiRibbonAction>
                {
                    new UiRibbonAction(
                        "BenchmarkSetup",
                        localizer["Ribbon_BenchmarkSetup"].Value,
                        "<svg><use href='/icons/sprite.svg#settings'/></svg>",
                        UiRibbonItemSize.Large,
                        false,
                        null,
                        () => { Navigation.NavigateTo("/card/setup?prefill=returnanalysis"); return Task.CompletedTask; }
                    )
                },
                Sort: 10
            ));
        }

        return new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs)
        };
    }
}


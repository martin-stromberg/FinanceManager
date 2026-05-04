using FinanceManager.Web.ViewModels.Common;

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
    }

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
}

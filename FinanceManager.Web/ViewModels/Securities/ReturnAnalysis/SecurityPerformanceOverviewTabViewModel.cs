using FinanceManager.Application.Securities.ReturnAnalysis;
using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Web.ViewModels.Securities.ReturnAnalysis;

/// <summary>
/// View model for the security performance overview tab.
/// </summary>
public sealed class SecurityPerformanceOverviewTabViewModel : BaseViewModel
{
    private static readonly ChartTimeRange[] RangesInternal =
    [
        ChartTimeRange.OneMonth,
        ChartTimeRange.ThreeMonths,
        ChartTimeRange.SixMonths,
        ChartTimeRange.OneYear,
        ChartTimeRange.ThreeYears,
        ChartTimeRange.All,
    ];

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="services">Service provider.</param>
    public SecurityPerformanceOverviewTabViewModel(IServiceProvider services) : base(services)
    {
    }

    /// <summary>
    /// Security identifier currently bound to this tab.
    /// </summary>
    public Guid SecurityId { get; private set; }

    /// <summary>
    /// Selected chart time range.
    /// </summary>
    public ChartTimeRange SelectedRange { get; private set; } = ChartTimeRange.OneYear;

    /// <summary>
    /// Available time ranges.
    /// </summary>
    public IReadOnlyList<ChartTimeRange> Ranges => RangesInternal;

    /// <summary>
    /// Loaded chart data.
    /// </summary>
    public PerformanceChartDataDto? Data { get; private set; }

    /// <summary>
    /// Gets whether loading is in progress.
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Loads chart data for the current selection.
    /// </summary>
    /// <param name="securityId">Security identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task LoadAsync(Guid securityId, CancellationToken ct = default)
    {
        SecurityId = securityId;
        await LoadCoreAsync(ct);
    }

    /// <summary>
    /// Selects a new range and reloads chart data.
    /// </summary>
    /// <param name="range">Range to load.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SelectRangeAsync(ChartTimeRange range, CancellationToken ct = default)
    {
        SelectedRange = range;
        await LoadCoreAsync(ct);
    }

    /// <summary>
    /// Returns short label for a chart range.
    /// </summary>
    /// <param name="range">Chart range.</param>
    /// <returns>Short display label.</returns>
    public static string GetRangeLabel(ChartTimeRange range) => range switch
    {
        ChartTimeRange.OneMonth => "1M",
        ChartTimeRange.ThreeMonths => "3M",
        ChartTimeRange.SixMonths => "6M",
        ChartTimeRange.OneYear => "1J",
        ChartTimeRange.ThreeYears => "3J",
        ChartTimeRange.All => "Gesamt",
        _ => range.ToString()
    };

    private async Task LoadCoreAsync(CancellationToken ct)
    {
        if (!CheckAuthentication())
        {
            return;
        }

        IsLoading = true;
        RaiseStateChanged();

        try
        {
            Data = await ApiClient.Securities_GetPerformanceChartAsync(SecurityId, SelectedRange, ct);
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode ?? string.Empty, ApiClient.LastError ?? ex.Message);
            Data = null;
        }
        finally
        {
            IsLoading = false;
            RaiseStateChanged();
        }
    }
}

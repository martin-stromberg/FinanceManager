using FinanceManager.Application.Securities.ReturnAnalysis;
using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Web.ViewModels.Securities.ReturnAnalysis;

/// <summary>
/// View model for the security performance metrics tab.
/// </summary>
public sealed class SecurityPerformanceMetricsTabViewModel : BaseViewModel
{
    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="services">Service provider.</param>
    public SecurityPerformanceMetricsTabViewModel(IServiceProvider services) : base(services)
    {
    }

    /// <summary>
    /// Loaded metrics data.
    /// </summary>
    public DetailedReturnMetricsDto? Data { get; private set; }

    /// <summary>
    /// Gets whether loading is in progress.
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Loads data for the given security.
    /// </summary>
    /// <param name="securityId">Security identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task LoadAsync(Guid securityId, CancellationToken ct = default)
    {
        if (!CheckAuthentication())
        {
            return;
        }

        IsLoading = true;
        RaiseStateChanged();

        try
        {
            Data = await ApiClient.Securities_GetReturnMetricsAsync(securityId, ct);
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

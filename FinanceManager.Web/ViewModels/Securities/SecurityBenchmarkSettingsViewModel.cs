using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Web.ViewModels.Securities;

/// <summary>
/// View model for the benchmark settings page.
/// Allows the user to select a security as benchmark for all return analysis comparisons.
/// </summary>
public sealed class SecurityBenchmarkSettingsViewModel : BaseViewModel
{
    /// <summary>Creates a new instance.</summary>
    /// <param name="services">Service provider.</param>
    public SecurityBenchmarkSettingsViewModel(IServiceProvider services) : base(services)
    {
    }

    /// <summary>Whether the data is being loaded.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Whether a save operation is in progress.</summary>
    public bool IsSaving { get; private set; }

    /// <summary>All securities available for selection as benchmark.</summary>
    public IReadOnlyList<SecurityDto> AvailableSecurities { get; private set; } = Array.Empty<SecurityDto>();

    /// <summary>Currently selected benchmark security id. Null means no benchmark.</summary>
    public Guid? SelectedBenchmarkSecurityId { get; set; }

    /// <summary>Loaded settings for display.</summary>
    public ReturnAnalysisSettingsResponse? CurrentSettings { get; private set; }

    /// <summary>Loads all securities and the current benchmark settings.</summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!CheckAuthentication())
        {
            return;
        }

        IsLoading = true;
        SetError(null, null);
        RaiseStateChanged();

        try
        {
            var securitiesTask = ApiClient.Securities_ListAsync(onlyActive: false, ct);
            var settingsTask = ApiClient.Securities_GetReturnAnalysisSettingsAsync(ct);

            await Task.WhenAll(securitiesTask, settingsTask);

            AvailableSecurities = securitiesTask.Result;
            CurrentSettings = settingsTask.Result;
            SelectedBenchmarkSecurityId = CurrentSettings?.BenchmarkSecurityId;
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode ?? string.Empty, ApiClient.LastError ?? ex.Message);
        }
        finally
        {
            IsLoading = false;
            RaiseStateChanged();
        }
    }

    /// <summary>
    /// Saves the selected benchmark security. Clears the benchmark when <see cref="SelectedBenchmarkSecurityId"/> is null.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True on success.</returns>
    public async Task<bool> SaveAsync(CancellationToken ct = default)
    {
        IsSaving = true;
        SetError(null, null);
        RaiseStateChanged();

        try
        {
            var req = new ReturnAnalysisSettingsUpdateRequest(
                SelectedBenchmarkSecurityId,
                CurrentSettings?.ShowSharpeRatio ?? false,
                CurrentSettings?.RiskFreeRate ?? 0m
            );

            var ok = await ApiClient.Securities_UpdateReturnAnalysisSettingsAsync(req, ct);
            if (ok)
            {
                CurrentSettings = new ReturnAnalysisSettingsResponse(
                    SelectedBenchmarkSecurityId,
                    AvailableSecurities.FirstOrDefault(s => s.Id == SelectedBenchmarkSecurityId)?.Name,
                    CurrentSettings?.ShowSharpeRatio ?? false,
                    CurrentSettings?.RiskFreeRate ?? 0m
                );
            }
            return ok;
        }
        catch (Exception ex)
        {
            SetError(ApiClient.LastErrorCode ?? string.Empty, ApiClient.LastError ?? ex.Message);
            return false;
        }
        finally
        {
            IsSaving = false;
            RaiseStateChanged();
        }
    }
}

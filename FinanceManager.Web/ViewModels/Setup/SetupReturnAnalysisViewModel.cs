using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Web.ViewModels.Setup;

/// <summary>
/// View model for the return analysis / benchmark settings section inside the setup card.
/// Loads the list of available securities and the current benchmark selection, and persists changes.
/// </summary>
public sealed class SetupReturnAnalysisViewModel : BaseViewModel
{
    private readonly ILogger<SetupReturnAnalysisViewModel> _logger;
    private ReturnAnalysisSettingsResponse? _currentSettings;

    /// <summary>Creates a new instance.</summary>
    /// <param name="sp">Service provider.</param>
    public SetupReturnAnalysisViewModel(IServiceProvider sp) : base(sp)
    {
        _logger = sp.GetRequiredService<ILogger<SetupReturnAnalysisViewModel>>();
    }

    /// <summary>Whether a load operation is in progress.</summary>
    public bool Loading { get; private set; }

    /// <summary>Whether a save operation is in progress.</summary>
    public bool Saving { get; private set; }

    /// <summary>True after a successful save.</summary>
    public bool SavedOk { get; private set; }

    /// <summary>Error message from the last load operation, if any.</summary>
    public string? Error { get; private set; }

    /// <summary>Error message from the last save operation, if any.</summary>
    public string? SaveError { get; private set; }

    /// <summary>All securities available for selection as benchmark.</summary>
    public IReadOnlyList<SecurityDto> AvailableSecurities { get; private set; } = Array.Empty<SecurityDto>();

    /// <summary>Currently selected benchmark security id. Null means no benchmark.</summary>
    public Guid? SelectedBenchmarkSecurityId { get; set; }

    /// <summary>
    /// Loads available securities and the current benchmark settings in parallel.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Loading return analysis settings");

        Loading = true;
        Error = null;
        SavedOk = false;
        RaiseStateChanged();

        try
        {
            var securitiesTask = ApiClient.Securities_ListAsync(onlyActive: false, ct);
            var settingsTask = ApiClient.Securities_GetReturnAnalysisSettingsAsync(ct);

            await Task.WhenAll(securitiesTask, settingsTask);

            AvailableSecurities = securitiesTask.Result;
            _currentSettings = settingsTask.Result;
            SelectedBenchmarkSecurityId = _currentSettings?.BenchmarkSecurityId;
        }
        catch (Exception ex)
        {
            Error = ApiClient.LastError ?? ex.Message;
        }
        finally
        {
            Loading = false;
            RaiseStateChanged();
        }
    }

    /// <summary>
    /// Saves the selected benchmark, preserving the existing Sharpe Ratio and risk-free rate values.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Saving benchmark settings, BenchmarkSecurityId={BenchmarkSecurityId}", SelectedBenchmarkSecurityId);

        Saving = true;
        SavedOk = false;
        SaveError = null;
        RaiseStateChanged();

        try
        {
            var req = new ReturnAnalysisSettingsUpdateRequest(
                SelectedBenchmarkSecurityId,
                _currentSettings?.ShowSharpeRatio ?? false,
                _currentSettings?.RiskFreeRate ?? 0m);

            var ok = await ApiClient.Securities_UpdateReturnAnalysisSettingsAsync(req, ct);

            if (ok)
            {
                _currentSettings = new ReturnAnalysisSettingsResponse(
                    SelectedBenchmarkSecurityId,
                    AvailableSecurities.FirstOrDefault(s => s.Id == SelectedBenchmarkSecurityId)?.Name,
                    _currentSettings?.ShowSharpeRatio ?? false,
                    _currentSettings?.RiskFreeRate ?? 0m);
                SavedOk = true;
            }
            else
            {
                SaveError = ApiClient.LastError ?? "Speichern fehlgeschlagen.";
            }
        }
        catch (Exception ex)
        {
            SaveError = ApiClient.LastError ?? ex.Message;
        }
        finally
        {
            Saving = false;
            RaiseStateChanged();
        }
    }
}

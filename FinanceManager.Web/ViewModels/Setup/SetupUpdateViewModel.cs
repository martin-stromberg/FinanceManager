#pragma warning disable CS1591
using FinanceManager.Shared.Dtos.Update;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Web.ViewModels.Setup;

public sealed class SetupUpdateViewModel : BaseViewModel
{
    private readonly ILogger<SetupUpdateViewModel>? _logger;

    public SetupUpdateViewModel(IServiceProvider sp) : base(sp)
    {
        _logger = sp.GetService<ILogger<SetupUpdateViewModel>>();
    }

    public UpdateSettingsDto? Settings { get; private set; }
    public UpdateStatusDto? Status { get; private set; }
    public bool Busy { get; private set; }
    public bool Installing { get; private set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        BeginBusy();
        try
        {
            Settings = await ApiClient.Updates_GetSettingsAsync(ct);
            Status = await ApiClient.Updates_GetStatusAsync(ct);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
        finally
        {
            Busy = false;
            RaiseStateChanged();
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (Settings is null)
        {
            return;
        }

        BeginBusy();
        try
        {
            Settings = await ApiClient.Updates_UpdateSettingsAsync(new UpdateSettingsUpdateRequest(
                Settings.Enabled,
                Settings.CheckIntervalMinutes,
                Settings.RepositoryOwner,
                Settings.RepositoryName,
                Settings.ManifestAssetName,
                Settings.ScheduledInstallTime,
                Settings.ServiceName,
                Settings.ExecutablePath,
                Settings.WorkingDirectory,
                Settings.HealthTimeoutSeconds), ct);
            Status = await ApiClient.Updates_GetStatusAsync(ct);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
        finally
        {
            Busy = false;
            RaiseStateChanged();
        }
    }

    public async Task CheckAsync(CancellationToken ct = default)
    {
        BeginBusy();
        try
        {
            var result = await ApiClient.Updates_CheckAsync(ct);
            Status = result.Status;
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
        finally
        {
            Busy = false;
            RaiseStateChanged();
        }
    }

    public async Task StartInstallAsync(bool confirmDowntime, CancellationToken ct = default)
    {
        BeginBusy();
        try
        {
            var status = await ApiClient.Updates_StartInstallAsync(new UpdateStartRequest(confirmDowntime), ct);
            if (status is not null)
            {
                Status = status;
                Installing = status.Status == UpdateStatusKind.Installing;
            }
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
        finally
        {
            Busy = false;
            RaiseStateChanged();
        }
    }

    public async Task ResetLockAsync(CancellationToken ct = default)
    {
        BeginBusy();
        try
        {
            await ApiClient.Updates_ResetLockAsync(new UpdateLockResetRequest("Reset from setup UI"), ct);
            Status = await ApiClient.Updates_GetStatusAsync(ct);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
        finally
        {
            Busy = false;
            RaiseStateChanged();
        }
    }

    public void UpdateSettings(UpdateSettingsDto settings)
    {
        Settings = settings;
        RaiseStateChanged();
    }

    public void MarkHealthTimeout()
    {
        SetError("Err_Update_HealthTimeout", "Die Anwendung wurde nach dem Update nicht innerhalb des erwarteten Zeitfensters erreichbar.");
        RaiseStateChanged();
    }

    private void BeginBusy()
    {
        Busy = true;
        SetError(null, null);
        RaiseStateChanged();
    }

    private void HandleException(Exception ex)
    {
        _logger?.LogError(ex, "Update setup operation failed.");
        SetError(ApiClient.LastErrorCode, ApiClient.LastError ?? ex.Message);
    }
}
#pragma warning restore CS1591

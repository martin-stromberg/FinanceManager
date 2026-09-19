using FinanceManager.Shared.Dtos.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FinanceManager.Web.ViewModels.Setup;

/// <summary>
/// View model for editing well-known endpoint settings in the setup area.
/// </summary>
public sealed class SetupWellKnownViewModel : BaseViewModel
{
    private readonly FinanceManager.Shared.IApiClient _api;
    private readonly ILogger<SetupWellKnownViewModel>? _logger;
    private WellKnownSettingsDto _original = new();
    private bool _busy;
    private bool _dirty;

    /// <summary>Creates a new instance.</summary>
    public SetupWellKnownViewModel(IServiceProvider sp) : base(sp)
    {
        _api = sp.GetRequiredService<FinanceManager.Shared.IApiClient>();
        _logger = sp.GetService<ILogger<SetupWellKnownViewModel>>();
    }

    /// <summary>Current editable settings.</summary>
    /// <returns>The result.</returns>
    public WellKnownSettingsDto Model { get; private set; } = new();

    /// <summary>Indicates whether the last load/save operation is in progress.</summary>
    public bool Busy
    {
        get => _busy;
        private set
        {
            if (_busy == value)
            {
                return;
            }

            _busy = value;
            RaiseStateChanged();
        }
    }

    /// <summary>Indicates whether the current values differ from the last loaded snapshot.</summary>
    public bool Dirty
    {
        get => _dirty;
        private set
        {
            if (_dirty == value)
            {
                return;
            }

            _dirty = value;
            RaiseStateChanged();
        }
    }

    /// <summary>Last error message.</summary>
    public string? Error { get; private set; }

    /// <summary>Last save error message.</summary>
    public string? SaveError { get; private set; }

    /// <summary>True when the last save operation completed successfully.</summary>
    public bool SavedOk { get; private set; }

    /// <summary>Loads the current settings.</summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        Busy = true;
        Error = null;
        SaveError = null;
        SavedOk = false;
        RaiseStateChanged();

        try
        {
            Model = await _api.GetWellKnownSettingsAsync(ct) ?? new WellKnownSettingsDto();
            _original = Clone(Model);
            RecomputeDirty();
        }
        catch (HttpRequestException ex)
        {
            Error = _api.LastError ?? ex.Message;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            Error = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogError(ex, "Loading well-known settings failed.");
            throw;
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Loading well-known settings failed.");
            throw;
        }
        catch (NotSupportedException ex)
        {
            _logger?.LogError(ex, "Loading well-known settings failed.");
            throw;
        }
        finally
        {
            Busy = false;
            RaiseStateChanged();
        }
    }

    /// <summary>Saves the current settings.</summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (!Dirty)
        {
            return;
        }

        Busy = true;
        SaveError = null;
        SavedOk = false;
        RaiseStateChanged();

        try
        {
            await _api.UpdateWellKnownSettingsAsync(new WellKnownSettingsUpdateRequest(Model.ChangePasswordUrl), ct);

            _original = Clone(Model);
            SavedOk = true;
            RecomputeDirty();
        }
        catch (HttpRequestException ex)
        {
            SaveError = _api.LastError ?? ex.Message;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            SaveError = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogError(ex, "Saving well-known settings failed.");
            throw;
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Saving well-known settings failed.");
            throw;
        }
        catch (NotSupportedException ex)
        {
            _logger?.LogError(ex, "Saving well-known settings failed.");
            throw;
        }
        finally
        {
            Busy = false;
            RaiseStateChanged();
        }
    }

    /// <summary>Marks the view model as changed.</summary>
    public void OnChanged()
    {
        SavedOk = false;
        SaveError = null;
        RecomputeDirty();
        RaiseStateChanged();
    }

    private void RecomputeDirty()
    {
        Dirty = Model.ChangePasswordUrl != _original.ChangePasswordUrl;
    }

    private static WellKnownSettingsDto Clone(WellKnownSettingsDto src) => new()
    {
        ChangePasswordUrl = src.ChangePasswordUrl
    };
}

using FinanceManager.Shared;
using Microsoft.Extensions.Localization;
using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Web.ViewModels.Setup;

/// <summary>
/// View model for the user profile settings editor in the setup area.
/// Provides loading, editing and persistence of <see cref="UserProfileSettingsDto"/>, API key handling and ribbon actions.
/// </summary>
public sealed class SetupProfileViewModel : BaseViewModel
{
    /// <summary>
    /// Initializes a new instance of <see cref="SetupProfileViewModel"/>.
    /// </summary>
    /// <param name="sp">Service provider used to resolve dependencies (API client, localizer, navigation, etc.).</param>
    public SetupProfileViewModel(IServiceProvider sp) : base(sp)
    {
    }

    /// <summary>
    /// Current editable model representing the user's profile settings.
    /// </summary>
    public UserProfileSettingsDto Model { get; private set; } = new();
    private UserProfileSettingsDto _original = new();

    /// <summary>
    /// Indicates whether a load operation is in progress.
    /// </summary>
    public bool Loading { get; private set; }

    /// <summary>
    /// Indicates whether a save operation is in progress.
    /// </summary>
    public bool Saving { get; private set; }

    /// <summary>
    /// True when the last save operation completed successfully.
    /// </summary>
    public bool SavedOk { get; private set; }

    /// <summary>
    /// Error message produced during load operations, if any.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Error message produced during save operations, if any.
    /// </summary>
    public string? SaveError { get; private set; }

    /// <summary>
    /// True when the model contains unsaved changes compared to the last loaded snapshot.
    /// </summary>
    public bool Dirty { get; private set; }

    /// <summary>
    /// Indicates whether an AlphaVantage API key is present for the user.
    /// </summary>
    public bool HasKey { get; private set; }

    /// <summary>
    /// Indicates whether the user chose to share their AlphaVantage API key.
    /// </summary>
    public bool ShareKey { get; set; }

    /// <summary>
    /// Temporary input field for entering an AlphaVantage API key.
    /// </summary>
    public string KeyInput { get; set; } = string.Empty;
    private bool _clearRequested;
    
    /// <summary>
    /// Loads the profile settings for the current user from the API and prepares the editable model.
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the operation.</param>
    /// <returns>A task that completes when the load operation has finished.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the provided cancellation token is canceled.</exception>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        Loading = true; Error = null; SaveError = null; SavedOk = false; RaiseStateChanged();
        try
        {
            var dto = await ApiClient.UserSettings_GetProfileAsync(ct);
            Model = dto ?? new();
            _original = Clone(Model);

            HasKey = dto?.HasAlphaVantageApiKey ?? false;
            ShareKey = dto?.ShareAlphaVantageApiKey ?? false;
            KeyInput = string.Empty;
            _clearRequested = false;

            RecomputeDirty();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally { Loading = false; RaiseStateChanged(); }
    }

    /// <summary>
    /// Persists pending profile changes to the API, including API key updates or clears.
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the operation.</param>
    /// <returns>A task that completes when the save operation has finished. The <see cref="SavedOk"/>, <see cref="SaveError"/> and <see cref="Dirty"/> state will be updated accordingly.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the provided cancellation token is canceled during the network call.</exception>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        Saving = true; SavedOk = false; SaveError = null; RaiseStateChanged();
        try
        {
            var request = new UserProfileSettingsUpdateRequest(
                PreferredLanguage: Model.PreferredLanguage,
                TimeZoneId: Model.TimeZoneId,
                AlphaVantageApiKey: string.IsNullOrWhiteSpace(KeyInput) ? null : KeyInput.Trim(),
                ClearAlphaVantageApiKey: _clearRequested ? true : null,
                ShareAlphaVantageApiKey: ShareKey
            );

            var ok = await ApiClient.UserSettings_UpdateProfileAsync(request, ct);
            if (ok)
            {
                Model.ShareAlphaVantageApiKey = ShareKey;
                _original = Clone(Model);
                HasKey = !_clearRequested && (HasKey || !string.IsNullOrWhiteSpace(KeyInput));
                KeyInput = string.Empty;
                _clearRequested = false;
                SavedOk = true;
                RecomputeDirty();
            }
            else
            {
                SaveError = ApiClient.LastError ?? "Save failed";
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SaveError = ex.Message;
        }
        finally { Saving = false; RaiseStateChanged(); }
    }

    /// <summary>
    /// Clears the API key input and marks the model to perform a key clear on save.
    /// </summary>
    public void ClearKey()
    {
        KeyInput = string.Empty;
        _clearRequested = true;
        OnChanged();
    }

    /// <summary>
    /// Resets the editable model to the most recently loaded values and clears pending key input.
    /// </summary>
    public void Reset()
    {
        Model = Clone(_original);
        SavedOk = false; SaveError = null;
        KeyInput = string.Empty;
        _clearRequested = false;
        ShareKey = _original.ShareAlphaVantageApiKey;
        RecomputeDirty();
        RaiseStateChanged();
    }

    /// <summary>
    /// Called when any editable field changed. Clears save state and recomputes dirty flag.
    /// </summary>
    public void OnChanged()
    {
        SavedOk = false;
        SaveError = null;
        RecomputeDirty();
        RaiseStateChanged();
    }

    /// <summary>
    /// Sets the detected language and timezone values (used by timezone detection UI) and applies length limits.
    /// </summary>
    /// <param name="lang">Detected language code (may be truncated).</param>
    /// <param name="tz">Detected timezone identifier (may be truncated).</param>
    public void SetDetectedTimezone(string? lang, string? tz)
    {
        if (!string.IsNullOrWhiteSpace(lang)) { Model.PreferredLanguage = lang[..Math.Min(lang.Length, 10)]; }
        if (!string.IsNullOrWhiteSpace(tz)) { Model.TimeZoneId = tz[..Math.Min(tz.Length, 100)]; }
        OnChanged();
    }

    private void RecomputeDirty()
    {
        var baseDirty = Model.PreferredLanguage != _original.PreferredLanguage || Model.TimeZoneId != _original.TimeZoneId;
        var keyDirty = !string.IsNullOrWhiteSpace(KeyInput) || _clearRequested || ShareKey != _original.ShareAlphaVantageApiKey;
        Dirty = baseDirty || keyDirty;
    }

    private static UserProfileSettingsDto Clone(UserProfileSettingsDto src) => new()
    {
        PreferredLanguage = src.PreferredLanguage,
        TimeZoneId = src.TimeZoneId,
        HasAlphaVantageApiKey = src.HasAlphaVantageApiKey,
        ShareAlphaVantageApiKey = src.ShareAlphaVantageApiKey
    };

    /// <summary>
    /// Builds ribbon register definitions for the profile editor including Save, Reset and Detect timezone actions.
    /// </summary>
    /// <param name="localizer">Localizer used to resolve UI labels and hints.</param>
    /// <returns>Collection of ribbon registers describing available tabs and actions for the UI.</returns>
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(Microsoft.Extensions.Localization.IStringLocalizer localizer)
    {
        var lblGroup = Localizer?["Ribbon_Group_Manage"].Value;
        var lblSave = Localizer?["Ribbon_Save"].Value;
        var lblReset = Localizer?["Ribbon_Reset"].Value;
        var lblActionsGroup = Localizer?["Ribbon_Group_Actions"].Value;
        var lblDetect = Localizer?["Ribbon_Detect_Timezone"].Value;

        var saveAction = new UiRibbonAction(
            "Save",
            lblSave,
            "<svg><use href='/icons/sprite.svg#save'/></svg>",
            UiRibbonItemSize.Small,
            !Dirty || Saving,
            null,
            "Save",
            new Func<Task>(async () => await SaveAsync())
        );

        var resetAction = new UiRibbonAction(
            "Reset",
            lblReset,
            "<svg><use href='/icons/sprite.svg#undo'/></svg>",
            UiRibbonItemSize.Small,
            !Dirty || Saving,
            null,
            "Reset",
            new Func<Task>(async () => { Reset(); await Task.CompletedTask; })
        );

        var detectAction = new UiRibbonAction(
            "DetectTimezone",
            lblDetect,
            "<svg><use href='/icons/sprite.svg#refresh'/></svg>",
            UiRibbonItemSize.Small,
            false,
            null,
            "DetectTimezone",
            new Func<Task>(async () => { RaiseUiActionRequested("DetectTimezone"); await Task.CompletedTask; })
        );

        var tabs = new List<UiRibbonTab>
        {
            new UiRibbonTab(lblGroup, new List<UiRibbonAction> { saveAction, resetAction }),
            new UiRibbonTab(lblActionsGroup, new List<UiRibbonAction> { detectAction })
        };

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }
}

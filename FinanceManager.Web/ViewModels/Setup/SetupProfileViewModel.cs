using FinanceManager.Shared;
using Microsoft.Extensions.Localization;
using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Web.ViewModels.Setup;

public sealed class SetupProfileViewModel : BaseViewModel
{
    public SetupProfileViewModel(IServiceProvider sp) : base(sp)
    {
    }

    public UserProfileSettingsDto Model { get; private set; } = new();
    private UserProfileSettingsDto _original = new();

    public bool Loading { get; private set; }
    public bool Saving { get; private set; }
    public bool SavedOk { get; private set; }
    public string? Error { get; private set; }
    public string? SaveError { get; private set; }
    public bool Dirty { get; private set; }

    public bool HasKey { get; private set; }
    public bool ShareKey { get; set; }
    public string KeyInput { get; set; } = string.Empty;
    private bool _clearRequested;
    
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
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally { Loading = false; RaiseStateChanged(); }
    }

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
        catch (Exception ex)
        {
            SaveError = ex.Message;
        }
        finally { Saving = false; RaiseStateChanged(); }
    }

    public void ClearKey()
    {
        KeyInput = string.Empty;
        _clearRequested = true;
        OnChanged();
    }

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

    public void OnChanged()
    {
        SavedOk = false;
        SaveError = null;
        RecomputeDirty();
        RaiseStateChanged();
    }

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

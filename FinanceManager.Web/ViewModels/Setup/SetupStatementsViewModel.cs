using FinanceManager.Shared;
using Microsoft.Extensions.Localization;
using System.Collections.Generic;

namespace FinanceManager.Web.ViewModels.Setup;

/// <summary>
/// View model for import split settings used in the setup area.
/// Provides loading, validation and persistence of the <see cref="ImportSplitSettingsDto"/> model and
/// exposes ribbon actions for Save and Reset.
/// </summary>
public sealed class SetupStatementsViewModel : BaseViewModel
{
    /// <summary>
    /// Initializes a new instance of <see cref="SetupStatementsViewModel"/>.
    /// </summary>
    /// <param name="sp">Service provider used to resolve framework services.</param>
    public SetupStatementsViewModel(IServiceProvider sp) : base(sp)
    {
    }

    /// <summary>
    /// Current model representing import split settings. May be <c>null</c> until <see cref="LoadAsync"/> completes.
    /// </summary>
    public ImportSplitSettingsDto? Model { get; private set; }
    private ImportSplitSettingsDto? _original;

    /// <summary>
    /// Indicates whether the view model is currently loading the model from the server.
    /// </summary>
    public bool Loading { get; private set; }

    /// <summary>
    /// Indicates whether the view model is currently saving changes to the server.
    /// </summary>
    public bool Saving { get; private set; }

    /// <summary>
    /// Indicates whether the last save operation completed successfully.
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
    /// Validation message key produced by <see cref="Validate"/> when model is invalid.
    /// </summary>
    public string? ValidationMessage { get; private set; }

    /// <summary>
    /// Indicates whether the current model has validation errors.
    /// </summary>
    public bool HasValidationError { get; private set; }

    /// <summary>
    /// Indicates whether the current model contains unsaved changes compared to the original loaded state.
    /// </summary>
    public bool Dirty { get; private set; }

    /// <summary>
    /// Loads the import split settings from the API and prepares the view model state.
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the operation.</param>
    /// <returns>A task that completes when the load operation has finished.</returns>
    /// <exception cref="OperationCanceledException">May be thrown if the provided cancellation token is cancelled by the caller or if underlying API calls observe cancellation.</exception>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        Loading = true; Error = null; SaveError = null; SavedOk = false; RaiseStateChanged();
        try
        {
            var dto = await ApiClient.UserSettings_GetImportSplitAsync(ct);
            Model = dto ?? new ImportSplitSettingsDto();
            _original = Clone(Model);
            RecomputeDirty();
            Validate();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally { Loading = false; RaiseStateChanged(); }
    }

    /// <summary>
    /// Persists the current model to the API after validation.
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the operation.</param>
    /// <returns>A task that completes when the save operation has finished.</returns>
    /// <remarks>
    /// When the save succeeds the internal original snapshot is updated and <see cref="SavedOk"/> is set to <c>true</c>.
    /// Validation is performed before any network call.
    /// </remarks>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (Model is null) { return; }
        Validate(); if (HasValidationError) { return; }
        Saving = true; SavedOk = false; SaveError = null; RaiseStateChanged();
        try
        {
            var request = new ImportSplitSettingsUpdateRequest(
                Mode: Model.Mode,
                MaxEntriesPerDraft: Model.MaxEntriesPerDraft,
                MonthlySplitThreshold: Model.MonthlySplitThreshold,
                MinEntriesPerDraft: Model.MinEntriesPerDraft
            );

            var ok = await ApiClient.UserSettings_UpdateImportSplitAsync(request, ct);
            if (ok)
            {
                _original = Clone(Model);
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

    /// <summary>
    /// Resets the editable model to the last loaded original values.
    /// </summary>
    public void Reset()
    {
        if (Model is null || _original is null) { return; }
        Model.Mode = _original.Mode;
        Model.MaxEntriesPerDraft = _original.MaxEntriesPerDraft;
        Model.MonthlySplitThreshold = _original.MonthlySplitThreshold;
        Model.MinEntriesPerDraft = _original.MinEntriesPerDraft;
        SavedOk = false; SaveError = null;
        Validate();
        RecomputeDirty();
        RaiseStateChanged();
    }

    /// <summary>
    /// Handler invoked when the split <see cref="ImportSplitMode"/> changes. Adjusts related fields and re-validates.
    /// </summary>
    public void OnModeChanged()
    {
        if (Model is not null && Model.Mode == ImportSplitMode.MonthlyOrFixed)
        {
            if (!Model.MonthlySplitThreshold.HasValue || Model.MonthlySplitThreshold.Value < Model.MaxEntriesPerDraft)
            {
                Model.MonthlySplitThreshold = Model.MaxEntriesPerDraft;
            }
        }
        Validate();
        RecomputeDirty();
        RaiseStateChanged();
    }

    /// <summary>
    /// Validates the current model and sets <see cref="ValidationMessage"/> and <see cref="HasValidationError"/> accordingly.
    /// </summary>
    public void Validate()
    {
        ValidationMessage = null; HasValidationError = false;
        if (Model is null) { return; }
        if (Model.MaxEntriesPerDraft < 20)
        {
            ValidationMessage = "ImportSplit_InvalidMaxMin20"; HasValidationError = true; return;
        }
        if (Model.Mode != ImportSplitMode.FixedSize)
        {
            if (Model.MinEntriesPerDraft < 1)
            {
                ValidationMessage = "ImportSplit_InvalidMinEntries"; HasValidationError = true; return;
            }
            if (Model.MinEntriesPerDraft > Model.MaxEntriesPerDraft)
            {
                ValidationMessage = "ImportSplit_InvalidMinGreaterMax"; HasValidationError = true; return;
            }
        }
        if (Model.Mode == ImportSplitMode.MonthlyOrFixed)
        {
            var thr = Model.MonthlySplitThreshold ?? 0;
            if (thr < Model.MaxEntriesPerDraft)
            {
                ValidationMessage = "ImportSplit_InvalidThreshold"; HasValidationError = true; return;
            }
        }
        RecomputeDirty();
    }

    private void RecomputeDirty()
    {
        if (Model is null || _original is null) { Dirty = false; return; }
        Dirty = Model.Mode != _original.Mode
             || Model.MaxEntriesPerDraft != _original.MaxEntriesPerDraft
             || (Model.MonthlySplitThreshold ?? 0) != (_original.MonthlySplitThreshold ?? 0)
             || Model.MinEntriesPerDraft != _original.MinEntriesPerDraft;
    }

    private static ImportSplitSettingsDto Clone(ImportSplitSettingsDto src) => new()
    {
        Mode = src.Mode,
        MaxEntriesPerDraft = src.MaxEntriesPerDraft,
        MonthlySplitThreshold = src.MonthlySplitThreshold,
        MinEntriesPerDraft = src.MinEntriesPerDraft
    };

    // Provide ribbon actions for this child ViewModel; parent/host will merge them automatically
    /// <summary>
    /// Builds ribbon register definitions exposing Save and Reset actions for the import split settings editor.
    /// </summary>
    /// <param name="localizer">Localizer used to resolve UI labels and hints.</param>
    /// <returns>A collection of <see cref="UiRibbonRegister"/> entries describing available ribbon tabs/actions.</returns>
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var actions = new List<UiRibbonAction>();

        // Save action
        actions.Add(new UiRibbonAction(
            "SaveImportSplit",
            localizer["Ribbon_Save"].Value,
            "<svg><use href='/icons/sprite.svg#save'/></svg>",
            UiRibbonItemSize.Large,
            false,
            localizer["Hint_Save"].Value ?? string.Empty,
            "SaveImportSplit",
            new Func<Task>(async () =>
            {
                try { await SaveAsync(); } catch { }
            })));

        // Reset action
        actions.Add(new UiRibbonAction(
            "ResetImportSplit",
            localizer["Ribbon_Reset"].Value,
            "<svg><use href='/icons/sprite.svg#undo'/></svg>",
            UiRibbonItemSize.Large,
            false,
            localizer["Hint_Reset"].Value ?? string.Empty,
            "ResetImportSplit",
            new Func<Task>(() => { Reset(); return Task.CompletedTask; })));

        var tabs = new List<UiRibbonTab>
        {
            new UiRibbonTab(localizer["Ribbon_Group_Manage"].Value, actions)
        };

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }
}

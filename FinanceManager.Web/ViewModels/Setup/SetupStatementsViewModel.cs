using FinanceManager.Shared;
using Microsoft.Extensions.Localization;
using System.Collections.Generic;

namespace FinanceManager.Web.ViewModels.Setup;

public sealed class SetupStatementsViewModel : BaseViewModel
{
    public SetupStatementsViewModel(IServiceProvider sp) : base(sp)
    {
    }

    public ImportSplitSettingsDto? Model { get; private set; }
    private ImportSplitSettingsDto? _original;

    public bool Loading { get; private set; }
    public bool Saving { get; private set; }
    public bool SavedOk { get; private set; }
    public string? Error { get; private set; }
    public string? SaveError { get; private set; }
    public string? ValidationMessage { get; private set; }
    public bool HasValidationError { get; private set; }
    public bool Dirty { get; private set; }

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

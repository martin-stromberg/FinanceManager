using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using FinanceManager.Shared.Dtos;
using Microsoft.Extensions.Localization;
using System.Collections.Generic;

namespace FinanceManager.Web.ViewModels.Setup;

/// <summary>
/// Card view model for the setup area. Provides UI state and actions for switching between
/// setup sections and for registering ribbon actions specific to the setup area.
/// </summary>
[FinanceManager.Web.ViewModels.Common.CardRoute("setup")]
public sealed class SetupCardViewModel : BaseCardViewModel<(string Key, string Value)>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetupCardViewModel"/> class.
    /// </summary>
    /// <param name="sp">The service provider used to resolve services required by the view model.</param>
    public SetupCardViewModel(IServiceProvider sp) : base(sp)
    {
    }

    /// <summary>
    /// Gets the localized title of the card.
    /// </summary>
    public override string Title => Localizer?["Setup_Title"] ?? "Setup";

    /// <summary>
    /// Currently selected settings section key. <c>null</c> when no section is selected.
    /// </summary>
    public string? SelectedSection { get; private set; }

    /// <summary>
    /// Exposes the available setting sections as (key, localized display name) pairs.
    /// The list is static and should be used to render the setup navigation.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> SettingSections => new List<KeyValuePair<string, string>>
    {
        new KeyValuePair<string, string>("profile", Localizer?["Setup_Section_Profile"].Value ?? "Profil"),
        new KeyValuePair<string, string>("notifications", Localizer?["Setup_Section_Notifications"].Value ?? "Benachrichtigungen"),
        new KeyValuePair<string, string>("statements", Localizer?["Setup_Section_Statements"].Value ?? "Kontoauszüge"),
        new KeyValuePair<string, string>("attachments", Localizer?["Setup_Section_Attachments"].Value ?? "Anhänge"),
        new KeyValuePair<string, string>("backup", Localizer?["Setup_Section_Backup"].Value ?? "Backup"),
        new KeyValuePair<string, string>("security", Localizer?["Setup_Section_Security"].Value ?? "Sicherheit"),
    };

    /// <summary>
    /// Changes the active setup view to the specified section key. If the key is invalid or blank the call is ignored.
    /// The method raises the necessary UI actions to render the embedded panel for the selected section.
    /// </summary>
    /// <param name="key">The section key to switch to (case-insensitive).</param>
    public void ChangeView(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        SelectedSection = key;
        RaiseStateChanged();

        RaiseUiActionRequested("ClearEmbeddedPanel", EmbeddedPanelPosition.AfterCard);
        if (string.Equals(key, "profile", StringComparison.OrdinalIgnoreCase))
        {
            RaisePanelUiAction<SetupProfileViewModel>(typeof(FinanceManager.Web.Components.Pages.Setup.SetupProfileTab));
        }
        else if (string.Equals(key, "notifications", StringComparison.OrdinalIgnoreCase))
        {
            RaisePanelUiAction<SetupNotificationsViewModel>(typeof(FinanceManager.Web.Components.Pages.Setup.SetupNotificationsTab));
        }
        else if (string.Equals(key, "statements", StringComparison.OrdinalIgnoreCase))
        {
            RaisePanelUiAction<SetupStatementsViewModel>(typeof(FinanceManager.Web.Components.Pages.Setup.SetupStatementTab));
        }
        else if (string.Equals(key, "attachments", StringComparison.OrdinalIgnoreCase))
        {
            RaisePanelUiAction<SetupAttachmentCategoriesViewModel>(typeof(FinanceManager.Web.Components.Pages.Setup.SetupAttachmentCategoriesTab));
        }
        else if (string.Equals(key, "backup", StringComparison.OrdinalIgnoreCase))
        {
            RaisePanelUiAction<SetupBackupsViewModel>(typeof(FinanceManager.Web.Components.Pages.Setup.SetupBackupTab));
        }
        else if (string.Equals(key, "security", StringComparison.OrdinalIgnoreCase))
        {
            RaisePanelUiAction<SetupSecurityViewModel>(typeof(FinanceManager.Web.Components.Pages.Setup.SetupSecurityTab));
        }
    }

    /// <summary>
    /// Gets the background task types that should be visible for this card.
    /// </summary>
    public override BackgroundTaskType[]? VisibleBackgroundTaskTypes => new BackgroundTaskType[] { BackgroundTaskType.RebuildAggregates, BackgroundTaskType.BackupRestore };

    /// <summary>
    /// Loads the card record and initializes the embedded panel for the setup UI.
    /// The method updates the <see cref="BaseViewModel.Loading"/> and error state and notifies the UI about state changes.
    /// </summary>
    /// <param name="id">Identifier of the record to load. For the setup card this value is ignored.</param>
    /// <returns>A task representing the asynchronous load operation.</returns>
    public override async Task LoadAsync(Guid id)
    {
        Loading = true; SetError(null, null); RaiseStateChanged();
        try
        {
            CardRecord = null;
            RaiseEmbeddedPanelUiAction();
        }
        catch (Exception ex)
        {
            SetError(null, ex.Message);
            CardRecord = null;
        }
        finally
        {
            Loading = false; RaiseStateChanged();
        }
    }

    private void RaiseEmbeddedPanelUiAction()
    {
        // Request embedded panel rendering for the setup sections component (placed after the ribbon)
        try
        {
            // inner parameters for the SetupSections component
            var innerParms = new Dictionary<string, object> { ["Provider"] = this } as IDictionary<string, object>;
            // outer parameters for the SetupPanel wrapper
            var outerParms = new Dictionary<string, object>
            {
                ["InnerComponentType"] = typeof(FinanceManager.Web.Components.Pages.SetupSections),
                ["InnerParameters"] = innerParms
            };

            var spec = new BaseViewModel.EmbeddedPanelSpec(typeof(FinanceManager.Web.Components.Shared.SetupPanel), outerParms, EmbeddedPanelPosition.AfterRibbon, true);
            RaiseUiActionRequested("EmbeddedPanel", spec);
        }
        catch { }
    }
    private void RaisePanelUiAction<T>(Type innerComponentType) where T :BaseViewModel
    {
        try
        {
            var profileVm = CreateSubViewModel<T>(true);
            var innerParms = new Dictionary<string, object> { ["ViewModel"] = profileVm } as IDictionary<string, object>;

            var outerParms = new Dictionary<string, object>
            {
                ["InnerComponentType"] = innerComponentType,
                ["InnerParameters"] = innerParms
            };
            var spec = new BaseViewModel.EmbeddedPanelSpec(typeof(FinanceManager.Web.Components.Shared.SetupPanel), outerParms, EmbeddedPanelPosition.AfterCard, true);
            RaiseUiActionRequested("EmbeddedPanel", spec);
        }
        catch { }
    }

    /// <summary>
    /// Determines whether a child view model is considered active for this card.
    /// Override used by the base card logic to control lifecycle of sub view models.
    /// </summary>
    /// <param name="vm">The child view model to check.</param>
    /// <returns><c>true</c> when the provided view model is active for the currently selected section; otherwise <c>false</c>.</returns>
    protected override bool IsChildViewModelActive(BaseViewModel vm)
    {
        return ((vm is SetupStatementsViewModel) && SelectedSection == "statements")
            || ((vm is SetupNotificationsViewModel) && SelectedSection == "notifications")
            || ((vm is SetupProfileViewModel) && SelectedSection == "profile")
            || ((vm is SetupBackupsViewModel) && SelectedSection == "backup")
            || ((vm is SetupSecurityViewModel) && SelectedSection == "security");
    }

    /// <summary>
    /// Provides the ribbon register definition for the setup card. Adds an Actions register with the RebuildAggregates action.
    /// </summary>
    /// <param name="localizer">Localizer used to obtain localized display text for ribbon items.</param>
    /// <returns>A list of <see cref="UiRibbonRegister"/> instances describing the ribbon layout, or <c>null</c> when no ribbon is needed.</returns>
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(Microsoft.Extensions.Localization.IStringLocalizer localizer)
    {
        // Provide a single Actions register with a tab containing the RebuildAggregates action
        var tabs = new List<UiRibbonTab>
        {
            new UiRibbonTab(localizer["Ribbon_Group_Actions"].Value, new List<UiRibbonAction>
            {
                new UiRibbonAction(
                    "RebuildAggregates",
                    localizer["Ribbon_RebuildAggregates"].Value,
                    "<svg><use href='/icons/sprite.svg#refresh'/></svg>",
                    UiRibbonItemSize.Large,
                    false,
                    localizer["Hint_RebuildAggregates"].Value,
                    new Func<Task>(async () =>
                    {
                        try
                        {
                            await ApiClient.Aggregates_RebuildAsync(allowDuplicate: false);
                        }
                        catch { }
                    }))
            }, int.MaxValue)
        };

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }

    /// <summary>
    /// Determines whether symbol upload is allowed for this card.
    /// The setup card does not allow symbol uploads.
    /// </summary>
    /// <returns><c>false</c> always for the setup card.</returns>
    protected override bool IsSymbolUploadAllowed() => false;

    /// <summary>
    /// Returns the attachment parent kind and parent id used when uploading symbols for this card.
    /// For the setup card a placeholder of statement draft with empty id is returned.
    /// </summary>
    /// <returns>A tuple containing the attachment entity kind and parent id.</returns>
    protected override (Domain.Attachments.AttachmentEntityKind Kind, Guid ParentId) GetSymbolParent()
        => (Domain.Attachments.AttachmentEntityKind.StatementDraft, Guid.Empty);

    /// <summary>
    /// Assigns a newly uploaded symbol to the current card record.
    /// For the setup card symbol assignment is not applicable and the method completes immediately.
    /// </summary>
    /// <param name="attachmentId">The id of the newly uploaded attachment, or <c>null</c> if none.</param>
    /// <returns>A completed task. No action is performed for the setup card.</returns>
    protected override Task AssignNewSymbolAsync(Guid? attachmentId) => Task.CompletedTask;
}

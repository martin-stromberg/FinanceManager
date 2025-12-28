using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using FinanceManager.Shared.Dtos;
using Microsoft.Extensions.Localization;
using System.Collections.Generic;

namespace FinanceManager.Web.ViewModels.Setup;

[FinanceManager.Web.ViewModels.Common.CardRoute("setup")]
public sealed class SetupCardViewModel : BaseCardViewModel<(string Key, string Value)>
{
    public SetupCardViewModel(IServiceProvider sp) : base(sp)
    {
    }

    public override string Title => Localizer?["Setup_Title"] ?? "Setup";

    // Currently selected settings section key
    public string? SelectedSection { get; private set; }

    // Expose the available setting sections (key, translated display name)
    public IReadOnlyList<KeyValuePair<string, string>> SettingSections => new List<KeyValuePair<string, string>>
    {
        new KeyValuePair<string, string>("profile", Localizer?["Setup_Section_Profile"].Value ?? "Profil"),
        new KeyValuePair<string, string>("notifications", Localizer?["Setup_Section_Notifications"].Value ?? "Benachrichtigungen"),
        new KeyValuePair<string, string>("statements", Localizer?["Setup_Section_Statements"].Value ?? "Kontoauszüge"),
        new KeyValuePair<string, string>("attachments", Localizer?["Setup_Section_Attachments"].Value ?? "Anhänge"),
        new KeyValuePair<string, string>("backup", Localizer?["Setup_Section_Backup"].Value ?? "Backup"),
        new KeyValuePair<string, string>("security", Localizer?["Setup_Section_Security"].Value ?? "Sicherheit"),
    };

    // Called by the UI to change the visible settings view
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
    public override BackgroundTaskType[]? VisibleBackgroundTaskTypes => new BackgroundTaskType[] { BackgroundTaskType.RebuildAggregates, BackgroundTaskType.BackupRestore };

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
    protected override bool IsChildViewModelActive(BaseViewModel vm)
    {
        return ((vm is SetupStatementsViewModel) && SelectedSection == "statements")
            || ((vm is SetupNotificationsViewModel) && SelectedSection == "notifications")
            || ((vm is SetupProfileViewModel) && SelectedSection == "profile")
            || ((vm is SetupBackupsViewModel) && SelectedSection == "backup")
            || ((vm is SetupSecurityViewModel) && SelectedSection == "security");
    }
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
                    "RebuildAggregates",
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

    protected override bool IsSymbolUploadAllowed() => false;
    protected override (Domain.Attachments.AttachmentEntityKind Kind, Guid ParentId) GetSymbolParent()
        => (Domain.Attachments.AttachmentEntityKind.StatementDraft, Guid.Empty);
    protected override Task AssignNewSymbolAsync(Guid? attachmentId) => Task.CompletedTask;
}

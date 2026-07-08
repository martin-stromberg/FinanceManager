using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FinanceManager.Shared.Dtos;
using Microsoft.Extensions.Localization;
using System.Collections.Generic;
using System.Linq;

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
        _logger = sp.GetService<ILogger<SetupCardViewModel>>();
    }

    private readonly ILogger<SetupCardViewModel>? _logger;

    private readonly Dictionary<string, BaseViewModel> _sectionViewModels = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<KeyValuePair<string, string>>? _settingSections;

    /// <summary>
    /// Raised when a section should be expanded programmatically. The event argument contains the section key.
    /// Subscribed by <c>SetupSections.razor</c> to expand the section before processing a pending action.
    /// </summary>
    public event EventHandler<string>? ExpandSectionRequested;

    /// <summary>
    /// Gets the localized title of the card.
    /// </summary>
    public override string Title => Localizer?["Setup_Title"] ?? "Setup";

    private static readonly SetupSectionDefinition[] SectionDefinitions =
    {
        new SetupSectionDefinition("profile", "Setup_Section_Profile", "Profil", typeof(SetupProfileViewModel), typeof(FinanceManager.Web.Components.Pages.Setup.SetupProfileTab)),
        new SetupSectionDefinition("notifications", "Setup_Section_Notifications", "Benachrichtigungen", typeof(SetupNotificationsViewModel), typeof(FinanceManager.Web.Components.Pages.Setup.SetupNotificationsTab)),
        new SetupSectionDefinition("statements", "Setup_Section_Statements", "Kontoauszüge", typeof(SetupStatementsViewModel), typeof(FinanceManager.Web.Components.Pages.Setup.SetupStatementTab)),
        new SetupSectionDefinition("attachments", "Setup_Section_Attachments", "Anhänge", typeof(SetupAttachmentCategoriesViewModel), typeof(FinanceManager.Web.Components.Pages.Setup.SetupAttachmentCategoriesTab)),
        new SetupSectionDefinition("backup", "Setup_Section_Backup", "Backup", typeof(SetupBackupsViewModel), typeof(FinanceManager.Web.Components.Pages.Setup.SetupBackupTab)),
        new SetupSectionDefinition("security", "Setup_Section_Security", "Sicherheit", typeof(SetupSecurityViewModel), typeof(FinanceManager.Web.Components.Pages.Setup.SetupSecurityTab)),
        new SetupSectionDefinition("returnanalysis", "Setup_Section_ReturnAnalysis", "Renditeanalyse", typeof(SetupReturnAnalysisViewModel), typeof(FinanceManager.Web.Components.Pages.Setup.SetupReturnAnalysisTab)),
    };

    /// <summary>
    /// Exposes the available setting sections as (key, localized display name) pairs.
    /// The list is materialized once in <see cref="LoadAsync"/> and cached for the lifetime of the view model.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> SettingSections => _settingSections ?? Array.Empty<KeyValuePair<string, string>>();

    /// <summary>
    /// Resolves the component type for a setup section key.
    /// </summary>
    /// <param name="key">Section key to resolve.</param>
    /// <param name="componentType">Resolved component type when found; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> when the section exists; otherwise <c>false</c>.</returns>
    public bool TryGetSectionComponentType(string key, out Type? componentType)
    {
        if (!TryGetSectionDefinition(key, out var sectionDefinition) || sectionDefinition is null)
        {
            componentType = null;
            return false;
        }

        componentType = sectionDefinition.ComponentType;
        return true;
    }

    /// <summary>
    /// Creates the setup section view model instance for a section key.
    /// Returns a pre-created, ribbon-contributing instance from the internal cache when available;
    /// otherwise creates a new instance via the service provider.
    /// </summary>
    /// <param name="key">Section key to resolve.</param>
    /// <param name="services">Service provider used for creating the view model instance.</param>
    /// <returns>The created view model instance, or <c>null</c> when the section key is unknown.</returns>
    public BaseViewModel? CreateSectionViewModel(string key, IServiceProvider services)
    {
        if (!TryGetSectionDefinition(key, out var sectionDefinition) || sectionDefinition is null)
        {
            return null;
        }

        if (_sectionViewModels.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var newVm = ActivatorUtilities.CreateInstance(services, sectionDefinition.ViewModelType);
        if (newVm is BaseViewModel baseVm)
        {
            // Intentionally not registered as a child view model via CreateSubViewModel<T>:
            // The sections resolved here at runtime (attachments, security, returnanalysis) do not
            // contribute ribbon actions, so omitting them from _childViewModels is by design.
            // Sections that do contribute ribbon actions (profile, notifications, backup, statements)
            // are pre-created in LoadAsync using CreateSubViewModel<T> which registers them properly.
            _sectionViewModels[key] = baseVm;
            return baseVm;
        }
        return null;
    }

    /// <summary>
    /// Gets the background task types that should be visible for this card.
    /// </summary>
    public override BackgroundTaskType[]? VisibleBackgroundTaskTypes => new BackgroundTaskType[] { BackgroundTaskType.RebuildAggregates, BackgroundTaskType.BackupRestore };

    /// <summary>
    /// Initializes the setup card. After loading, automatically selects the section specified by
    /// <see cref="BaseCardViewModel{T}.InitPrefill"/> when a valid section key was provided via the
    /// <c>?prefill=</c> query parameter.
    /// </summary>
    /// <param name="id">Identifier of the record. Ignored for the setup card.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    public override async Task InitializeAsync(Guid id)
    {
        await LoadAsync(id);
    }

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

            if (_sectionViewModels.Count == 0)
            {
                var profileVm = CreateSubViewModel<SetupProfileViewModel>();
                _sectionViewModels["profile"] = profileVm;

                var notificationsVm = CreateSubViewModel<SetupNotificationsViewModel>();
                _sectionViewModels["notifications"] = notificationsVm;

                var backupVm = CreateSubViewModel<SetupBackupsViewModel>(configure: vm =>
                    vm.BeforeUploadCallback = () => ExpandSectionRequested?.Invoke(this, "backup"));
                _sectionViewModels["backup"] = backupVm;

                var statementsVm = CreateSubViewModel<SetupStatementsViewModel>();
                _sectionViewModels["statements"] = statementsVm;
            }

            _settingSections = SectionDefinitions
                .Select(section => new KeyValuePair<string, string>(section.Key, Localizer?[section.LocalizationKey].Value ?? section.FallbackTitle))
                .ToList();

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
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to raise embedded panel UI action");
        }
    }
    private static bool TryGetSectionDefinition(string key, out SetupSectionDefinition? sectionDefinition)
    {
        sectionDefinition = SectionDefinitions.FirstOrDefault(section => string.Equals(section.Key, key, StringComparison.OrdinalIgnoreCase));
        return sectionDefinition is not null;
    }

    private sealed class SetupSectionDefinition
    {
        public SetupSectionDefinition(string key, string localizationKey, string fallbackTitle, Type viewModelType, Type componentType)
        {
            Key = key;
            LocalizationKey = localizationKey;
            FallbackTitle = fallbackTitle;
            ViewModelType = viewModelType;
            ComponentType = componentType;
        }

        public string Key { get; }
        public string LocalizationKey { get; }
        public string FallbackTitle { get; }
        public Type ViewModelType { get; }
        public Type ComponentType { get; }
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
                        catch (Exception ex) { _logger?.LogError(ex, "RebuildAggregates ribbon action failed"); }
                    })),
                new UiRibbonAction(
                    "ResetReportCache",
                    localizer["Ribbon_ResetReportCache"].Value,
                    "<svg><use href='/icons/sprite.svg#delete'/></svg>",
                    UiRibbonItemSize.Small,
                    false,
                    localizer["Hint_ResetReportCache"].Value,
                    new Func<Task>(async () =>
                    {
                        try
                        {
                            await ApiClient.Budgets_ResetReportCacheAsync();
                            await ApiClient.Securities_ResetReturnCacheAsync();
                            SetError(null, localizer["Info_ResetReportCache"].Value ?? "Report cache reset.");
                            RaiseStateChanged();
                        }
                        catch (Exception ex) { _logger?.LogError(ex, "ResetReportCache ribbon action failed"); }
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

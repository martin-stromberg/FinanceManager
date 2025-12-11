using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Setup;

public sealed class SetupViewModel : ViewModelBase
{
    public const string TabBackup = "backup";
    public const string TabImportSplit = "import-split";
    public const string TabNotifications = "notifications";
    public const string TabProfile = "profile";
    public const string TabIpBlocks = "ip-blocks";
    public const string TabAttachmentCategories = "attachment-categories";

    public SetupViewModel(IServiceProvider sp) : base(sp) { }

    public string ActiveSection { get; private set; } = TabBackup;

    public void ApplyQueryFromUri(string uri)
    {
        try
        {
            var parsed = new Uri(uri);
            var query = QueryHelpers.ParseQuery(parsed.Query);
            if (query.TryGetValue("section", out var sectionVals))
            {
                var section = sectionVals.ToString();
                if (IsValidSection(section))
                {
                    ActiveSection = section;
                    RaiseStateChanged();
                    return;
                }
            }
            if (query.ContainsKey("focus"))
            {
                ActiveSection = TabIpBlocks;
                RaiseStateChanged();
            }
        }
        catch
        {
            // ignore malformed query
        }
    }

    public void Activate(string section)
    {
        if (IsValidSection(section) && section != ActiveSection)
        {
            ActiveSection = section;
            RaiseStateChanged();
        }
    }

    public bool IsActiveSection(string section) => ActiveSection == section;

    public static bool IsValidSection(string section)
        => section == TabBackup
        || section == TabImportSplit
        || section == TabNotifications
        || section == TabProfile
        || section == TabIpBlocks
        || section == TabAttachmentCategories;

    // Ribbon: provide registers/tabs/actions via the new provider API
    public override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        var tabs = new List<UiRibbonTab>
        {
            new UiRibbonTab(localizer["Ribbon_Group_Navigation"], new List<UiRibbonAction>
            {
                new UiRibbonAction(
                    Id: "Back",
                    Label: localizer["Ribbon_Back"],
                    IconSvg: "<svg><use href='/icons/sprite.svg#back'/></svg>",
                    Size: UiRibbonItemSize.Large,
                    Disabled: false,
                    Tooltip: null,
                    Action: "Back",
                    Callback: new Func<Task>(() => { RaiseUiActionRequested("Back"); return Task.CompletedTask; })
                )
            })
        };

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }
}

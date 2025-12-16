namespace FinanceManager.Web.ViewModels.Common;

public enum UiRibbonItemSize
{
    Small,
    Large
}

public enum UiRibbonRegisterKind
{
    QuickAccess,
    Actions,
    LinkedInfo,
    Reports,
    Custom
}

public sealed record UiRibbonAction(
    string Id,
    string Label,
    string IconSvg,
    UiRibbonItemSize Size,
    bool Disabled,
    string? Tooltip,
    string? Action,
    Func<Task>? Callback
);

public sealed record UiRibbonTab(string Title, List<UiRibbonAction> Items);

public sealed record UiRibbonRegister(UiRibbonRegisterKind Kind, List<UiRibbonTab>? Tabs)
{
    // Compatibility properties used by older tests / code expecting a flat register shape
    public string Title => (Tabs != null && Tabs.Count > 0) ? Tabs[0].Title : string.Empty;

    public List<UiRibbonAction> Items => (Tabs != null && Tabs.Count > 0) ? Tabs[0].Items : new List<UiRibbonAction>();
}

// Legacy compatibility types used by older viewmodels/components
public sealed record UiRibbonItem(
    string Label,
    string IconSvg,
    UiRibbonItemSize Size,
    bool Disabled,
    string Action,
    string? Tooltip = null,
    Func<Task>? Callback = null
);

public sealed record UiRibbonGroup(string Title, List<UiRibbonItem> Items);
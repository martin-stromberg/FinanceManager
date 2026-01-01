using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Common;

/// <summary>
/// Helper extension methods to work with ribbon definitions exposed by view models.
/// Contains compatibility helpers and conversion utilities used by the UI layer to convert
/// ribbon registers into UI groups and items.
/// </summary>
public static class RibbonExtensions
{
    /// <summary>
    /// Compatibility helper used by legacy callers and tests to obtain ribbon registers from an arbitrary view model instance.
    /// If the supplied <paramref name="vm"/> implements <see cref="IRibbonProvider"/> the provider's
    /// <see cref="IRibbonProvider.GetRibbonRegisters(Microsoft.Extensions.Localization.IStringLocalizer)"/> is invoked.
    /// </summary>
    /// <param name="vm">View model object which may implement <see cref="IRibbonProvider"/>. May be <c>null</c>.</param>
    /// <param name="localizer">Localizer instance used to resolve labels when the provider builds register definitions.</param>
    /// <returns>
    /// A read-only list of <see cref="UiRibbonRegister"/> instances when the view model implements <see cref="IRibbonProvider"/>,
    /// otherwise <c>null</c>.
    /// </returns>
    public static IReadOnlyList<UiRibbonRegister>? GetRibbon(this object? vm, IStringLocalizer localizer)
    {
        if (vm is IRibbonProvider rp) return rp.GetRibbonRegisters(localizer);
        return null;
    }

    /// <summary>
    /// Converts a collection of <see cref="UiRibbonRegister"/> into UI-friendly <see cref="UiRibbonGroup"/> instances
    /// that are rendered by the client. The conversion flattens registers and their tabs into groups and items.
    /// </summary>
    /// <param name="regs">Ribbon register definitions to convert. May be <c>null</c>.</param>
    /// <param name="localizer">Optional localizer that may be used by callers when resolving labels; currently unused by the converter.</param>
    /// <returns>A list of <see cref="UiRibbonGroup"/> instances. Never <c>null</c>; returns an empty list when <paramref name="regs"/> is <c>null</c> or contains no tabs.</returns>
    public static IReadOnlyList<UiRibbonGroup> ToUiRibbonGroups(this IReadOnlyList<UiRibbonRegister>? regs, IStringLocalizer? localizer = null)
    {
        if (regs == null) return new List<UiRibbonGroup>();
        var groups = new List<UiRibbonGroup>();
        foreach (var reg in regs)
        {
            if (reg?.Tabs == null) { continue; }
            foreach (var tab in reg.Tabs)
            {
                if (tab == null) { continue; }
                var items = new List<UiRibbonItem>();
                var actions = tab.Items ?? new List<UiRibbonAction>();
                foreach (var a in actions)
                {
                    if (a == null) { continue; }
                    var actionName = a.Action ?? a.Id;
                    items.Add(new UiRibbonItem(
                        a.Label,
                        a.IconSvg,
                        a.Size,
                        a.Disabled,
                        actionName,
                        a.Tooltip,
                        a.Callback
                    ));
                }
                groups.Add(new UiRibbonGroup(tab.Title, items));
            }
        }
        return groups;
    }
}

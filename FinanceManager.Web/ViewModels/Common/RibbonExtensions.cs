using Microsoft.Extensions.Localization;
using System.Linq;

namespace FinanceManager.Web.ViewModels.Common;

public static class RibbonExtensions
{
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

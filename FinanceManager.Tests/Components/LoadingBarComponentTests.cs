using Bunit;
using Microsoft.Extensions.DependencyInjection;
using msTools.Web.Blazor;

namespace FinanceManager.Tests.Components;

/// <summary>
/// Verifies that the <c>LoadingBar</c> component (from the shared <c>msTools.Web.Blazor</c> loading-bar
/// infrastructure) renders using the appearance and identity options the hosting application configured via
/// <c>AddLoadingBar</c>, rather than the library's built-in defaults.
/// </summary>
public sealed class LoadingBarComponentTests : BunitContext
{
    /// <summary>
    /// Verifies that all host-configured loading bar options - element id, extra CSS class, color list, height,
    /// top offset, mobile top offset and z-index - are actually applied to the rendered element's class list,
    /// <c>data-loading-colors</c> attribute and inline style. Guards against options being silently dropped or
    /// only partially wired through to the markup when the host customizes the bar's appearance.
    /// </summary>
    [Fact]
    public void LoadingBar_UsesHostConfiguredAppearance()
    {
        Services.AddLoadingBar(options =>
        {
            options.ElementId = "host-loading";
            options.CssClass = "host-bar";
            options.Colors = new[] { "#111111", "#222222" };
            options.Height = "4px";
            options.Top = "2px";
            options.MobileTop = "48px";
            options.ZIndex = 2500;
        });

        var cut = Render<LoadingBar>();
        var bar = cut.Find("#host-loading");

        Assert.Contains("mst-loading-bar", bar.ClassList);
        Assert.Contains("host-bar", bar.ClassList);
        Assert.Equal("#111111,#222222", bar.GetAttribute("data-loading-colors"));
        Assert.Contains("--mst-loading-bar-height:4px", bar.GetAttribute("style"));
        Assert.Contains("--mst-loading-bar-top:2px", bar.GetAttribute("style"));
        Assert.Contains("--mst-loading-bar-mobile-top:48px", bar.GetAttribute("style"));
        Assert.Contains("--mst-loading-bar-z-index:2500", bar.GetAttribute("style"));
    }
}

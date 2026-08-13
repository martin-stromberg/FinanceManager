using Bunit;
using Microsoft.Extensions.DependencyInjection;
using msTools.Web.Blazor;

namespace FinanceManager.Tests.Components;

public sealed class LoadingBarComponentTests : BunitContext
{
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

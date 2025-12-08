using Bunit;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Tests.Components;

public class RibbonTests : TestContext
{
    private enum TabId { One, Two }

    [Fact]
    public void SingleTab_RendersGroupsAndButtons()
    {
        // Arrange
        var registers = new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>
            {
                new UiRibbonTab("Tab One", new List<UiRibbonAction>
                {
                    new UiRibbonAction("save","Save","<svg></svg>", UiRibbonItemSize.Small, false, null, null, null),
                    new UiRibbonAction("delete","Delete","<svg></svg>", UiRibbonItemSize.Small, true, null, null, null)
                })
            })
        };

        // Act
        var cut = Render<Ribbon<TabId>>(p => p
            .Add(x => x.Registers, registers)
            .Add(x => x.ActiveTab, TabId.One));

        // Assert
        Assert.Equal(1, cut.FindAll(".fm-ribbon-group").Count);
        Assert.Contains("Tab One", cut.Markup);
        var buttons = cut.FindAll("button.fm-ribbon-btn");
        Assert.Equal(2, buttons.Count);
        Assert.Null(buttons[0].GetAttribute("aria-disabled"));
        Assert.Equal("true", buttons[1].GetAttribute("aria-disabled"));
    }

    [Fact]
    public async Task ClickCallback_IsInvoked()
    {
        // Arrange
        var clicked = false;
        var action = new UiRibbonAction("run","Run","<svg></svg>", UiRibbonItemSize.Small, false, null, "run", new Func<Task>(() => { clicked = true; return Task.CompletedTask; }));
        var registers = new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>
            {
                new UiRibbonTab("Tab One", new List<UiRibbonAction> { action })
            })
        };

        var cut = Render<Ribbon<TabId>>(p => p
            .Add(x => x.Registers, registers)
            .Add(x => x.ActiveTab, TabId.One));

        // Act
        cut.Find("button.fm-ribbon-btn").Click();

        // Assert
        Assert.True(clicked);
    }
}

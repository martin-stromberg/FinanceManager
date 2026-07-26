using Bunit;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.ViewModels.Common;
using Moq;
using Microsoft.Extensions.Localization;
using FinanceManager.Web.ViewModels;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Tests.Components;

public class RibbonTests : Bunit.BunitContext
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
                    new UiRibbonAction("save","Save","<svg></svg>", UiRibbonItemSize.Small, false, null, null),
                    new UiRibbonAction("delete","Delete","<svg></svg>", UiRibbonItemSize.Small, true, null, null)
                })
            })
        };

        var provMock = new Mock<IRibbonProvider>();
        provMock.Setup(p => p.GetRibbonRegisters(It.IsAny<IStringLocalizer>())).Returns(registers);

        var localMock = new Mock<IStringLocalizer>();

        RenderFragment frag = builder =>
        {
            builder.OpenComponent(0, typeof(Ribbon<TabId>));
            builder.AddAttribute(1, "Provider", provMock.Object);
            builder.AddAttribute(2, "Localizer", localMock.Object);
            builder.CloseComponent();
        };

        var cut = Render(frag);

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
        var action = new UiRibbonAction("run","Run","<svg></svg>", UiRibbonItemSize.Small, false, null, new Func<Task>(() => { clicked = true; return Task.CompletedTask; }));
        var registers = new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>
            {
                new UiRibbonTab("Tab One", new List<UiRibbonAction> { action })
            })
        };

        var provMock = new Mock<IRibbonProvider>();
        provMock.Setup(p => p.GetRibbonRegisters(It.IsAny<IStringLocalizer>())).Returns(registers);

        var localMock = new Mock<IStringLocalizer>();

        RenderFragment frag = builder =>
        {
            builder.OpenComponent(0, typeof(Ribbon<TabId>));
            builder.AddAttribute(1, "Provider", provMock.Object);
            builder.AddAttribute(2, "Localizer", localMock.Object);
            builder.CloseComponent();
        };

        var cut = Render(frag);

        // Act
        cut.Find("button.fm-ribbon-btn").Click();

        // Assert
        Assert.True(clicked);
    }

    [Fact]
    public void MobileGroupPanel_RendersGroupTitleAndHamburgerButton()
    {
        var registers = new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>
            {
                new UiRibbonTab("Aktionen", new List<UiRibbonAction>
                {
                    new UiRibbonAction("save","Speichern","<svg></svg>", UiRibbonItemSize.Small, false, null, null)
                })
            })
        };

        var provMock = new Mock<IRibbonProvider>();
        provMock.Setup(p => p.GetRibbonRegisters(It.IsAny<IStringLocalizer>())).Returns(registers);
        var localMock = new Mock<IStringLocalizer>();

        RenderFragment frag = builder =>
        {
            builder.OpenComponent(0, typeof(Ribbon<TabId>));
            builder.AddAttribute(1, "Provider", provMock.Object);
            builder.AddAttribute(2, "Localizer", localMock.Object);
            builder.CloseComponent();
        };

        var cut = Render(frag);

        Assert.Single(cut.FindAll(".fm-ribbon-mobile-group-panel"));
        Assert.Equal("Aktionen", cut.Find(".fm-ribbon-mobile-group-title").TextContent.Trim());
        Assert.Single(cut.FindAll(".fm-ribbon-mobile-group-hamburger"));
    }

    [Fact]
    public void MobileGroupMenu_TogglesOnHamburgerClick()
    {
        var registers = new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>
            {
                new UiRibbonTab("Aktionen", new List<UiRibbonAction>
                {
                    new UiRibbonAction("save","Speichern","<svg></svg>", UiRibbonItemSize.Small, false, null, null)
                })
            })
        };

        var provMock = new Mock<IRibbonProvider>();
        provMock.Setup(p => p.GetRibbonRegisters(It.IsAny<IStringLocalizer>())).Returns(registers);
        var localMock = new Mock<IStringLocalizer>();

        RenderFragment frag = builder =>
        {
            builder.OpenComponent(0, typeof(Ribbon<TabId>));
            builder.AddAttribute(1, "Provider", provMock.Object);
            builder.AddAttribute(2, "Localizer", localMock.Object);
            builder.CloseComponent();
        };

        var cut = Render(frag);

        var menu = cut.Find(".fm-ribbon-mobile-menu");
        Assert.DoesNotContain("open", menu.ClassList);

        cut.Find(".fm-ribbon-mobile-group-toggle").Click();

        menu = cut.Find(".fm-ribbon-mobile-menu");
        Assert.Contains("open", menu.ClassList);
    }

    [Fact]
    public void MobileGroupMenu_ItemsRenderIconAndName()
    {
        var registers = new List<UiRibbonRegister>
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>
            {
                new UiRibbonTab("Aktionen", new List<UiRibbonAction>
                {
                    new UiRibbonAction("save", "Speichern", "<svg><path d='M0 0'></path></svg>", UiRibbonItemSize.Small, false, null, null),
                    new UiRibbonAction("delete", "Löschen", "<svg><circle cx='4' cy='4' r='2'></circle></svg>", UiRibbonItemSize.Small, false, null, null)
                })
            })
        };

        var provMock = new Mock<IRibbonProvider>();
        provMock.Setup(p => p.GetRibbonRegisters(It.IsAny<IStringLocalizer>())).Returns(registers);
        var localMock = new Mock<IStringLocalizer>();

        RenderFragment frag = builder =>
        {
            builder.OpenComponent(0, typeof(Ribbon<TabId>));
            builder.AddAttribute(1, "Provider", provMock.Object);
            builder.AddAttribute(2, "Localizer", localMock.Object);
            builder.CloseComponent();
        };

        var cut = Render(frag);
        cut.Find(".fm-ribbon-mobile-group-toggle").Click();

        var menuItems = cut.FindAll(".fm-ribbon-mobile-menu.open .fm-ribbon-mobile-menu-item");
        Assert.Equal(2, menuItems.Count);

        Assert.Equal("Speichern", menuItems[0].QuerySelector(".text-inline")?.TextContent.Trim());
        Assert.NotNull(menuItems[0].QuerySelector(".icon svg"));

        Assert.Equal("Löschen", menuItems[1].QuerySelector(".text-inline")?.TextContent.Trim());
        Assert.NotNull(menuItems[1].QuerySelector(".icon svg"));
    }

    [Fact]
    public void MobileShortcut_ExplicitAction_RendersIconOnlyInClosedHeader()
    {
        var registers = CreateRegisters(new List<UiRibbonAction>
        {
            new UiRibbonAction("save", "Speichern", "<svg><path d='M0 0'></path></svg>", UiRibbonItemSize.Small, false, "Jetzt speichern", null)
            {
                MobileShortcut = true
            },
            new UiRibbonAction("delete", "Löschen", "<svg></svg>", UiRibbonItemSize.Small, false, null, null)
        });

        var cut = RenderRibbon(registers);

        var shortcut = cut.Find("#save-mobile-shortcut");
        Assert.Contains("fm-ribbon-mobile-shortcut", shortcut.ClassList);
        Assert.Equal("Speichern", shortcut.GetAttribute("aria-label"));
        Assert.Equal("Jetzt speichern", shortcut.GetAttribute("title"));
        Assert.NotNull(shortcut.QuerySelector(".icon svg"));
        Assert.Null(shortcut.QuerySelector(".text"));
        Assert.Null(shortcut.QuerySelector(".text-inline"));
        Assert.Equal(string.Empty, shortcut.TextContent.Trim());
    }

    [Fact]
    public void MobileShortcut_ClickInvokesCallbackWithoutOpeningGroup()
    {
        var clicked = false;
        var registers = CreateRegisters(new List<UiRibbonAction>
        {
            new UiRibbonAction("save", "Speichern", "<svg></svg>", UiRibbonItemSize.Small, false, null, () =>
            {
                clicked = true;
                return Task.CompletedTask;
            })
            {
                MobileShortcut = true
            },
            new UiRibbonAction("delete", "Löschen", "<svg></svg>", UiRibbonItemSize.Small, false, null, null)
        });

        var cut = RenderRibbon(registers);

        cut.Find("#save-mobile-shortcut").Click();

        Assert.True(clicked);
        Assert.DoesNotContain("open", cut.Find(".fm-ribbon-mobile-menu").ClassList);
    }

    [Fact]
    public void MobileShortcut_OpenGroupHidesShortcuts()
    {
        var registers = CreateRegisters(new List<UiRibbonAction>
        {
            new UiRibbonAction("save", "Speichern", "<svg></svg>", UiRibbonItemSize.Small, false, null, null)
            {
                MobileShortcut = true
            },
            new UiRibbonAction("delete", "Löschen", "<svg></svg>", UiRibbonItemSize.Small, false, null, null)
        });

        var cut = RenderRibbon(registers);

        Assert.Single(cut.FindAll(".fm-ribbon-mobile-shortcut"));

        cut.Find(".fm-ribbon-mobile-group-toggle").Click();

        Assert.Empty(cut.FindAll(".fm-ribbon-mobile-shortcut"));
    }

    [Fact]
    public void MobileShortcut_SingleVisibleNonFileAction_IsAutomaticShortcut()
    {
        var registers = CreateRegisters(new List<UiRibbonAction>
        {
            new UiRibbonAction("save", "Speichern", "<svg></svg>", UiRibbonItemSize.Small, false, null, null)
        });

        var cut = RenderRibbon(registers);

        Assert.Single(cut.FindAll("#save-mobile-shortcut"));
    }

    [Fact]
    public void MobileShortcut_MultipleActionsWithoutMarking_RenderNoShortcut()
    {
        var registers = CreateRegisters(new List<UiRibbonAction>
        {
            new UiRibbonAction("save", "Speichern", "<svg></svg>", UiRibbonItemSize.Small, false, null, null),
            new UiRibbonAction("delete", "Löschen", "<svg></svg>", UiRibbonItemSize.Small, false, null, null)
        });

        var cut = RenderRibbon(registers);

        Assert.Empty(cut.FindAll(".fm-ribbon-mobile-shortcut"));
    }

    [Fact]
    public void MobileShortcut_HiddenAction_IsNotRenderedAsShortcut()
    {
        var registers = CreateRegisters(new List<UiRibbonAction>
        {
            new UiRibbonAction("hidden", "Versteckt", "<svg></svg>", UiRibbonItemSize.Small, false, null, null)
            {
                Hidden = true,
                MobileShortcut = true
            },
            new UiRibbonAction("visible", "Sichtbar", "<svg></svg>", UiRibbonItemSize.Small, false, null, null)
        });

        var cut = RenderRibbon(registers);

        Assert.Empty(cut.FindAll("#hidden-mobile-shortcut"));
        Assert.Single(cut.FindAll("#visible-mobile-shortcut"));
    }

    [Fact]
    public void MobileShortcut_DisabledAction_RendersDisabled()
    {
        var registers = CreateRegisters(new List<UiRibbonAction>
        {
            new UiRibbonAction("save", "Speichern", "<svg></svg>", UiRibbonItemSize.Small, true, null, null)
            {
                MobileShortcut = true
            },
            new UiRibbonAction("delete", "Löschen", "<svg></svg>", UiRibbonItemSize.Small, false, null, null)
        });

        var cut = RenderRibbon(registers);

        var shortcut = cut.Find("#save-mobile-shortcut");
        Assert.True(shortcut.HasAttribute("disabled"));
        Assert.Equal("true", shortcut.GetAttribute("aria-disabled"));
    }

    [Fact]
    public void MobileShortcut_FileCallbackAction_IsNotRenderedAsShortcut()
    {
        var registers = CreateRegisters(new List<UiRibbonAction>
        {
            new UiRibbonAction("import", "Importieren", "<svg></svg>", UiRibbonItemSize.Small, false, null, null)
            {
                FileCallback = _ => Task.CompletedTask,
                MobileShortcut = true
            },
            new UiRibbonAction("delete", "Löschen", "<svg></svg>", UiRibbonItemSize.Small, false, null, null)
        });

        var cut = RenderRibbon(registers);

        Assert.Empty(cut.FindAll("#import-mobile-shortcut"));
        Assert.Empty(cut.FindAll(".fm-ribbon-mobile-shortcut"));
        Assert.Single(cut.FindAll("#import-mobile"));
    }

    private IRenderedComponent<Ribbon<TabId>> RenderRibbon(List<UiRibbonRegister> registers)
    {
        var provMock = new Mock<IRibbonProvider>();
        provMock.Setup(p => p.GetRibbonRegisters(It.IsAny<IStringLocalizer>())).Returns(registers);
        var localMock = new Mock<IStringLocalizer>();

        return Render<Ribbon<TabId>>(parameters => parameters
            .Add(p => p.Provider, provMock.Object)
            .Add(p => p.Localizer, localMock.Object));
    }

    private static List<UiRibbonRegister> CreateRegisters(List<UiRibbonAction> actions) =>
        new()
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>
            {
                new UiRibbonTab("Aktionen", actions)
            })
        };
}

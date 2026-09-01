using Bunit;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.ViewModels.Common;
using Moq;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;
using FinanceManager.Web.ViewModels;
using msTools.Web.Blazor;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Tests.Components;

/// <summary>
/// Tests for the generic <see cref="Ribbon{TTabId}"/> toolbar component: rendering of desktop groups and
/// buttons, forwarding of an action's click callback, and the responsive mobile view (hamburger menu,
/// per-action "shortcut" buttons shown in the collapsed header, and the rules that decide which actions
/// are automatically or explicitly promoted to shortcuts).
/// </summary>
public class RibbonTests : Bunit.BunitContext
{
    /// <summary>Placeholder tab-id type, needed only to satisfy the generic <see cref="Ribbon{TTabId}"/> type parameter; its values are never used by the tests.</summary>
    private enum TabId { One, Two }

    public RibbonTests()
    {
        Services.AddScoped<LoadingBarService>();
        JSInterop.SetupVoid("financeManager.loadingBar.start").SetVoidResult();
        JSInterop.SetupVoid("financeManager.loadingBar.stop").SetVoidResult();
    }

    /// <summary>
    /// A single tab with two actions renders one ribbon group container and one button per action, and
    /// the disabled action is marked with <c>aria-disabled="true"</c> while the enabled one carries no
    /// such attribute.
    /// </summary>
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

    /// <summary>
    /// Clicking a rendered ribbon button invokes the <see cref="Func{Task}"/> callback that the
    /// corresponding <see cref="UiRibbonAction"/> was configured with, confirming the component wires
    /// DOM clicks through to the action's own delegate rather than swallowing them.
    /// </summary>
    [Fact]
    public async Task ClickCallback_IsInvoked()
    {
        // Arrange
        var clicked = false;
        var action = new UiRibbonAction("run", "Run", "<svg></svg>", UiRibbonItemSize.Small, false, null, new Func<Task>(() => { clicked = true; return Task.CompletedTask; }));
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

    /// <summary>
    /// In the mobile layout, the collapsed group panel shows the tab's title text and a hamburger toggle
    /// button, even before the menu has ever been opened.
    /// </summary>
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

    /// <summary>
    /// Clicking the mobile hamburger toggle adds the "open" CSS class to the mobile menu, which is what
    /// drives its visibility - the menu starts closed and opens purely from local component state.
    /// </summary>
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

    /// <summary>
    /// Once opened, each action in the mobile menu renders both its label text and its SVG icon, and does
    /// so for every action in order - not just the first one.
    /// </summary>
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

    /// <summary>
    /// An action explicitly marked <c>MobileShortcut = true</c> renders as an icon-only button in the
    /// collapsed mobile header (no visible label text), while still exposing its name via
    /// <c>aria-label</c> and its tooltip via <c>title</c> - shortcuts must stay accessible despite the
    /// compact, icon-only presentation.
    /// </summary>
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
            {
                MobileShortcut = true
            }
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

    /// <summary>
    /// Clicking a mobile shortcut button invokes its action callback directly, without also expanding the
    /// full mobile group menu - shortcuts are meant as a one-tap path, not an alternate way to open the menu.
    /// </summary>
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
            {
                MobileShortcut = true
            }
        });

        var cut = RenderRibbon(registers);

        cut.Find("#save-mobile-shortcut").Click();

        Assert.True(clicked);
        Assert.DoesNotContain("open", cut.Find(".fm-ribbon-mobile-menu").ClassList);
    }

    /// <summary>
    /// Once the full mobile group menu is opened, the header's shortcut buttons disappear, avoiding two
    /// redundant ways to trigger the same action being visible at once.
    /// </summary>
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

    /// <summary>
    /// When a tab has exactly one visible, enabled, non-file action and none is explicitly marked as a
    /// shortcut, that single action is still auto-promoted to a mobile shortcut - there is no ambiguity
    /// about which action the user would want quick access to.
    /// </summary>
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

    /// <summary>
    /// The single-visible-action auto-shortcut rule also applies to file-upload actions (those with a
    /// <c>FileCallback</c>), not just plain click actions.
    /// </summary>
    [Fact]
    public void MobileShortcut_SingleVisibleFileAction_IsAutomaticShortcut()
    {
        var registers = CreateRegisters(new List<UiRibbonAction>
        {
            new UiRibbonAction("import", "Importieren", "<svg></svg>", UiRibbonItemSize.Small, false, null, null)
            {
                FileCallback = _ => Task.CompletedTask
            }
        });

        var cut = RenderRibbon(registers);

        Assert.Single(cut.FindAll("#import-mobile-shortcut"));
    }

    /// <summary>
    /// A single action that is disabled is not auto-promoted to a mobile shortcut, even though it is the
    /// only action present - a shortcut the user can't actually activate would be misleading.
    /// </summary>
    [Fact]
    public void MobileShortcut_SingleDisabledAction_IsNotAutomaticShortcut()
    {
        var registers = CreateRegisters(new List<UiRibbonAction>
        {
            new UiRibbonAction("save", "Speichern", "<svg></svg>", UiRibbonItemSize.Small, true, null, null)
        });

        var cut = RenderRibbon(registers);

        Assert.Empty(cut.FindAll("#save-mobile-shortcut"));
    }

    /// <summary>
    /// With more than one visible action and none explicitly marked <c>MobileShortcut</c>, no shortcut is
    /// auto-selected at all - the auto-promotion rule only applies to the single-action case, since
    /// picking one of several actions to favor would be an arbitrary guess.
    /// </summary>
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

    /// <summary>
    /// An action marked <c>Hidden = true</c> is never rendered as a mobile shortcut even if it is also
    /// marked <c>MobileShortcut = true</c>, while a sibling visible action still renders as its own
    /// shortcut - hidden actions must stay fully invisible, not just absent from the desktop group.
    /// </summary>
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

    /// <summary>
    /// A disabled action marked <c>MobileShortcut = true</c> is not rendered as a shortcut, while another
    /// enabled action also marked as a shortcut still renders - disabled actions are filtered out of the
    /// shortcut bar individually, not just as part of the single-action auto-promotion rule.
    /// </summary>
    [Fact]
    public void MobileShortcut_DisabledAction_IsNotRenderedAsShortcut()
    {
        var registers = CreateRegisters(new List<UiRibbonAction>
        {
            new UiRibbonAction("save", "Speichern", "<svg></svg>", UiRibbonItemSize.Small, true, null, null)
            {
                MobileShortcut = true
            },
            new UiRibbonAction("delete", "Löschen", "<svg></svg>", UiRibbonItemSize.Small, false, null, null)
            {
                MobileShortcut = true
            }
        });

        var cut = RenderRibbon(registers);

        Assert.Empty(cut.FindAll("#save-mobile-shortcut"));
        Assert.Single(cut.FindAll("#delete-mobile-shortcut"));
    }

    /// <summary>
    /// A file-upload action rendered as a mobile shortcut still carries a real
    /// <c>input[type="file"]</c> element (not just a plain button), and the underlying full-size file
    /// input in the desktop group remains present alongside it - the shortcut is a second entry point to
    /// the same file picker, not a replacement for it.
    /// </summary>
    [Fact]
    public void MobileShortcut_FileCallbackAction_RendersUploadShortcut()
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

        var shortcut = cut.Find("#import-mobile-shortcut");
        Assert.Contains("fm-ribbon-mobile-shortcut", shortcut.ClassList);
        Assert.NotNull(shortcut.QuerySelector("input[type=\"file\"]"));
        Assert.Single(cut.FindAll("#import-mobile"));
    }

    /// <summary>
    /// In the mobile header's DOM order, the title/toggle element comes first, then the shortcut buttons
    /// container, then the hamburger expand button - shortcuts must sit between the title and the expand
    /// control so they read as quick actions rather than being mistaken for part of the expand affordance.
    /// </summary>
    [Fact]
    public void MobileShortcut_HeaderPlacesShortcutsBeforeExpandButton()
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

        var header = cut.Find(".fm-ribbon-mobile-group-header");
        var children = header.Children.Select(child => child.ClassName).ToList();
        Assert.Equal("fm-ribbon-mobile-group-title-toggle", children[0]);
        Assert.Equal("fm-ribbon-mobile-shortcuts", children[1]);
        Assert.Equal("fm-ribbon-mobile-group-toggle", children[2]);
    }

    /// <summary>
    /// Renders a <see cref="Ribbon{TabId}"/> backed by a mocked <see cref="IRibbonProvider"/> that returns
    /// the given registers, and a no-op localizer. Used by the mobile-shortcut tests to avoid repeating
    /// the provider/localizer wiring for every scenario.
    /// </summary>
    private IRenderedComponent<Ribbon<TabId>> RenderRibbon(List<UiRibbonRegister> registers)
    {
        var provMock = new Mock<IRibbonProvider>();
        provMock.Setup(p => p.GetRibbonRegisters(It.IsAny<IStringLocalizer>())).Returns(registers);
        var localMock = new Mock<IStringLocalizer>();

        return Render<Ribbon<TabId>>(parameters => parameters
            .Add(p => p.Provider, provMock.Object)
            .Add(p => p.Localizer, localMock.Object));
    }

    /// <summary>
    /// Wraps the given actions in a single "Aktionen" tab inside a single <see cref="UiRibbonRegister"/>,
    /// which is the minimal register/tab structure the <see cref="Ribbon{TabId}"/> component needs to
    /// render a mobile group panel.
    /// </summary>
    private static List<UiRibbonRegister> CreateRegisters(List<UiRibbonAction> actions) =>
        new()
        {
            new UiRibbonRegister(UiRibbonRegisterKind.Actions, new List<UiRibbonTab>
            {
                new UiRibbonTab("Aktionen", actions)
            })
        };
}

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

        cut.Find(".fm-ribbon-mobile-group-header").Click();

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
        cut.Find(".fm-ribbon-mobile-group-header").Click();

        var menuItems = cut.FindAll(".fm-ribbon-mobile-menu.open .fm-ribbon-mobile-menu-item");
        Assert.Equal(2, menuItems.Count);

        Assert.Equal("Speichern", menuItems[0].QuerySelector(".text-inline")?.TextContent.Trim());
        Assert.NotNull(menuItems[0].QuerySelector(".icon svg"));

        Assert.Equal("Löschen", menuItems[1].QuerySelector(".text-inline")?.TextContent.Trim());
        Assert.NotNull(menuItems[1].QuerySelector(".icon svg"));
    }
}

using Bunit;
using FinanceManager.Web.Components.Shared;

namespace FinanceManager.Tests.Components.Shared;

/// <summary>
/// Tests for <see cref="KpiInfoButton"/>'s open/close state, driven by its <c>Toggle</c> and <c>Close</c>
/// methods, and for the accessibility wiring between the dialog and its heading.
/// </summary>
public sealed class KpiInfoButtonTests_OpenClose : BunitContext
{
    private IRenderedComponent<KpiInfoButton> RenderButton(string title = "Volatility")
        => Render<KpiInfoButton>(builder => builder
            .Add(c => c.Title, title)
            .Add(c => c.CloseLabel, "Close")
            .AddChildContent("<p>Explanation</p>"));

    /// <summary>
    /// Initially, the explanation dialog is not rendered at all.
    /// </summary>
    [Fact]
    public void InitialState_DialogIsNotRendered()
    {
        var cut = RenderButton();

        Assert.Empty(cut.FindAll(".kpi-explanation-overlay"));
    }

    /// <summary>
    /// Clicking the info button toggles the dialog open, showing the title as the heading.
    /// </summary>
    [Fact]
    public void Toggle_ClickInfoButtonWhenClosed_OpensDialogWithTitleHeading()
    {
        var cut = RenderButton("Volatility");

        cut.Find(".kpi-info-btn").Click();

        Assert.Equal("Volatility", cut.Find(".kpi-explanation-header h3").TextContent);
    }

    /// <summary>
    /// Clicking the info button a second time toggles the dialog closed again.
    /// </summary>
    [Fact]
    public void Toggle_ClickInfoButtonTwice_ClosesDialogAgain()
    {
        var cut = RenderButton();

        cut.Find(".kpi-info-btn").Click();
        cut.Find(".kpi-info-btn").Click();

        Assert.Empty(cut.FindAll(".kpi-explanation-overlay"));
    }

    /// <summary>
    /// Clicking the dedicated close button inside the open dialog closes it.
    /// </summary>
    [Fact]
    public void Close_ClickCloseButtonWhileOpen_ClosesDialog()
    {
        var cut = RenderButton();

        cut.Find(".kpi-info-btn").Click();
        cut.Find(".kpi-explanation-header .icon-btn").Click();

        Assert.Empty(cut.FindAll(".kpi-explanation-overlay"));
    }

    /// <summary>
    /// While open, the dialog's <c>aria-labelledby</c> attribute references the <c>id</c> of the heading
    /// that carries the title, so assistive technology announces the correct name for the dialog.
    /// </summary>
    [Fact]
    public void Toggle_WhenOpen_DialogAriaLabelledByReferencesHeadingId()
    {
        var cut = RenderButton();

        cut.Find(".kpi-info-btn").Click();

        var overlay = cut.Find(".kpi-explanation-overlay");
        var heading = cut.Find(".kpi-explanation-header h3");
        var labelledBy = overlay.GetAttribute("aria-labelledby");

        Assert.False(string.IsNullOrWhiteSpace(labelledBy));
        Assert.Equal(labelledBy, heading.GetAttribute("id"));
    }
}

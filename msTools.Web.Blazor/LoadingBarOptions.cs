namespace msTools.Web.Blazor;

/// <summary>
/// Configures the reusable global loading bar component.
/// </summary>
public sealed class LoadingBarOptions
{
    /// <summary>
    /// Gets or sets the DOM id used by the JavaScript bridge.
    /// </summary>
    public string ElementId { get; set; } = "fm-loading-bar";

    /// <summary>
    /// Gets or sets optional host-specific CSS classes.
    /// </summary>
    public string? CssClass { get; set; }

    /// <summary>
    /// Gets or sets the host-provided color palette used on each restart.
    /// </summary>
    public IReadOnlyList<string> Colors { get; set; } = new[] { "currentColor" };

    /// <summary>
    /// Gets or sets the bar height as CSS length.
    /// </summary>
    public string Height { get; set; } = "3px";

    /// <summary>
    /// Gets or sets the desktop top offset as CSS length.
    /// </summary>
    public string Top { get; set; } = "0";

    /// <summary>
    /// Gets or sets the mobile top offset as CSS length.
    /// </summary>
    public string MobileTop { get; set; } = "0";

    /// <summary>
    /// Gets or sets the maximum viewport width at which the mobile offset applies.
    /// </summary>
    public string MobileBreakpoint { get; set; } = "900px";

    /// <summary>
    /// Gets or sets the z-index used by the loading bar.
    /// </summary>
    public int ZIndex { get; set; } = 1200;
}

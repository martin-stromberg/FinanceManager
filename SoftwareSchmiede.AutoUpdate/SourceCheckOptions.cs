namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Configures the periodic background check of the configured <see cref="IAutoUpdateSource"/>.
/// </summary>
public sealed class SourceCheckOptions
{
    /// <summary>
    /// Gets or sets the interval, in minutes, between successive source checks. Must be at least 1.
    /// </summary>
    public int Interval { get; set; } = 360;

    /// <summary>
    /// Gets or sets the time windows within which checks are allowed to run. An empty list means checks are
    /// allowed at any time.
    /// </summary>
    public List<SourceCheckTimeRange> TimeRanges { get; set; } = new();
}

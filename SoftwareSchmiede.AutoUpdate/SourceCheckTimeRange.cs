namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Describes a single allowed time window for periodic source checks on a given day of week.
/// </summary>
public sealed class SourceCheckTimeRange
{
    /// <summary>
    /// Gets or sets the day of week this time range applies to.
    /// </summary>
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>
    /// Gets or sets the inclusive start time of the window.
    /// </summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>
    /// Gets or sets the exclusive end time of the window.
    /// </summary>
    public TimeOnly EndTime { get; set; }
}

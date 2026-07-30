namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Determines whether a given point in time falls within a configured set of <see cref="SourceCheckTimeRange"/>
/// windows. An empty set of ranges means checks are always allowed.
/// </summary>
public sealed class SourceCheckWindowEvaluator
{
    /// <summary>
    /// Determines whether <paramref name="now"/> falls within any of <paramref name="timeRanges"/>.
    /// </summary>
    /// <param name="timeRanges">The configured time windows. An empty collection always returns <see langword="true"/>.</param>
    /// <param name="now">The point in time to evaluate, in the local time zone.</param>
    /// <returns><see langword="true"/> if checks are allowed at <paramref name="now"/>; otherwise <see langword="false"/>.</returns>
    public bool IsWithinWindow(IReadOnlyList<SourceCheckTimeRange> timeRanges, DateTimeOffset now)
    {
        if (timeRanges.Count == 0)
        {
            return true;
        }

        var dayOfWeek = now.DayOfWeek;
        var timeOfDay = TimeOnly.FromDateTime(now.DateTime);
        return timeRanges.Any(range =>
            range.DayOfWeek == dayOfWeek &&
            timeOfDay >= range.StartTime &&
            timeOfDay < range.EndTime);
    }
}

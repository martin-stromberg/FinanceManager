namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Determines whether a scheduled installation should be triggered for a given status snapshot, point in time
/// and record of the last attempted schedule, used by <see cref="AutoUpdateSchedulerService"/>.
/// </summary>
public sealed class ScheduledInstallEvaluator
{
    /// <summary>
    /// Determines whether a scheduled installation should be triggered.
    /// </summary>
    /// <param name="scheduledTime">The configured scheduled install time, or <see langword="null"/> if none is configured.</param>
    /// <param name="snapshot">The current status snapshot.</param>
    /// <param name="now">The current local time.</param>
    /// <param name="lastAttemptedDate">The date the last scheduled installation attempt was made, if any.</param>
    /// <param name="lastAttemptedTime">The scheduled time of the last scheduled installation attempt, if any.</param>
    /// <returns><see langword="true"/> if installation should be triggered now.</returns>
    public bool ShouldInstall(
        TimeOnly? scheduledTime,
        AutoUpdateStatusSnapshot snapshot,
        DateTimeOffset now,
        DateOnly? lastAttemptedDate,
        TimeOnly? lastAttemptedTime)
    {
        if (!scheduledTime.HasValue || snapshot.State != AutoUpdateState.ReadyToInstall || snapshot.IsLocked)
        {
            return false;
        }

        var today = DateOnly.FromDateTime(now.DateTime);
        if (lastAttemptedDate == today && lastAttemptedTime == scheduledTime.Value)
        {
            return false;
        }

        return TimeOnly.FromDateTime(now.DateTime) >= scheduledTime.Value;
    }
}

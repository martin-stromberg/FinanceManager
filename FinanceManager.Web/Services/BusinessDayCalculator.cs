namespace FinanceManager.Web.Services;

/// <summary>
/// Utility helpers for computing business days. Currently considers weekends (Saturday/Sunday) non-business days
/// and supports optional holiday provider checks. Holiday calendar integration may be extended in the future.
/// </summary>
public static class BusinessDayCalculator
{
    // Simple: weekends only. Extend with holiday calendar later.

    /// <summary>
    /// Returns the last business day for the month containing the supplied UTC date/time.
    /// </summary>
    /// <param name="utcNow">A UTC date/time used to determine the month. The value's <see cref="DateTime.Kind"/> is ignored.</param>
    /// <returns>A <see cref="DateTime"/> representing the last business day (local date portion) of the month.</returns>
    public static DateTime GetLastBusinessDayUtc(DateTime utcNow)
        => GetLastBusinessDay(utcNow);

    /// <summary>
    /// Returns the last business day (Monday through Friday) of the month for the given calendar date.
    /// The input is treated as a calendar date (no timezone conversion); <see cref="DateTime.Kind"/> is ignored.
    /// Weekends (Saturday, Sunday) are skipped. Holidays are not considered by this method.
    /// </summary>
    /// <param name="date">A <see cref="DateTime"/> whose month is used to determine the last business day.</param>
    /// <returns>The last business day of the month as a <see cref="DateTime"/> with the date component set to the business day.</returns>
    public static DateTime GetLastBusinessDay(DateTime date)
    {
        var monthStart = new DateTime(date.Year, date.Month, 1);
        var nextMonth = monthStart.AddMonths(1);
        var last = nextMonth.AddDays(-1);
        while (last.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            last = last.AddDays(-1);
        }
        return last.Date;
    }

    /// <summary>
    /// Determines if the provided date is a business day. Weekends (Saturday/Sunday) are considered non-business days.
    /// When a holiday provider is supplied, public holidays returned by the provider are also treated as non-business days.
    /// </summary>
    /// <param name="date">The date to evaluate. The time portion is ignored.</param>
    /// <param name="holidays">Optional holiday provider used to determine public holidays. May be <c>null</c>.</param>
    /// <param name="countryCode">Optional ISO country code used by the holiday provider.</param>
    /// <param name="subdivisionCode">Optional subdivision/region code used by the holiday provider.</param>
    /// <returns><c>true</c> when the date is a business day; otherwise <c>false</c>.</returns>
    public static bool IsBusinessDay(DateTime date, FinanceManager.Application.Notifications.IHolidayProvider? holidays, string? countryCode = null, string? subdivisionCode = null)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }
        if (holidays != null && holidays.IsPublicHoliday(date, countryCode, subdivisionCode))
        {
            return false;
        }
        return true;
    }
}

namespace FinanceManager.Domain.Users;

public sealed partial class User
{
    // --- Notification settings ---

    /// <summary>
    /// Whether monthly reminder emails/notifications are enabled for the user.
    /// </summary>
    /// <value><c>true</c> when monthly reminders are enabled; otherwise <c>false</c>.</value>
    public bool MonthlyReminderEnabled { get; private set; } = false;

    /// <summary>
    /// Local time (user time zone) hour for monthly reminder. Null => default 09:00.
    /// </summary>
    /// <value>Hour of day in range 0..23 or <c>null</c> for default.</value>
    public int? MonthlyReminderHour { get; private set; }

    /// <summary>
    /// Local time (user time zone) minute for monthly reminder. Null => default 00.
    /// </summary>
    /// <value>Minute in range 0..59 or <c>null</c> for default.</value>
    public int? MonthlyReminderMinute { get; private set; }

    /// <summary>
    /// ISO 3166-1 alpha-2 country code for public holiday calendar (e.g., "DE"). Optional.
    /// </summary>
    /// <value>Country code or <c>null</c> when not configured.</value>
    public string? HolidayCountryCode { get; private set; }

    /// <summary>
    /// ISO 3166-2 subdivision/state code (e.g., "DE-BY"). Optional.
    /// </summary>
    /// <value>Subdivision code or <c>null</c> when not configured.</value>
    public string? HolidaySubdivisionCode { get; private set; }

    /// <summary>
    /// Enables or disables notification settings that are applied on a monthly basis.
    /// </summary>
    /// <param name="monthlyReminderEnabled">True to enable monthly reminders; false to disable.</param>
    public void SetNotificationSettings(bool monthlyReminderEnabled)
    {
        MonthlyReminderEnabled = monthlyReminderEnabled;
        Touch();
    }

    /// <summary>
    /// Sets the preferred local time (hour and minute) for the monthly reminder notification.
    /// </summary>
    /// <param name="hour">Hour of day in the range 0..23, or <c>null</c> to use the default hour (09).</param>
    /// <param name="minute">Minute in the range 0..59, or <c>null</c> to use the default minute (00).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="hour"/> is outside 0..23 or <paramref name="minute"/> is outside 0..59.</exception>
    public void SetMonthlyReminderTime(int? hour, int? minute)
    {
        if (hour.HasValue)
        {
            if (hour.Value < 0 || hour.Value > 23)
            {
                throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23.");
            }
        }
        if (minute.HasValue)
        {
            if (minute.Value < 0 || minute.Value > 59)
            {
                throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59.");
            }
        }
        MonthlyReminderHour = hour;
        MonthlyReminderMinute = minute;
        Touch();
    }

    /// <summary>
    /// Sets the holiday region used to resolve public holidays for scheduling (country and optional subdivision).
    /// </summary>
    /// <param name="countryCode">ISO 3166-1 alpha-2 country code (e.g., "DE") or <c>null</c> to clear.</param>
    /// <param name="subdivisionCode">ISO 3166-2 subdivision code (e.g., "DE-BY") or <c>null</c> to clear.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when provided codes are shorter or longer than allowed lengths.</exception>
    public void SetHolidayRegion(string? countryCode, string? subdivisionCode)
    {
        HolidayCountryCode = Normalize(countryCode, 2, 10);
        HolidaySubdivisionCode = Normalize(subdivisionCode, 2, 20);
        Touch();

        static string? Normalize(string? s, int min, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) { return null; }
            var v = s.Trim();
            if (v.Length < min || v.Length > max) { throw new ArgumentOutOfRangeException(nameof(s)); }
            return v.ToUpperInvariant();
        }
    }

    /// <summary>
    /// The holiday provider kind used to obtain holiday calendars for this user.
    /// </summary>
    /// <value>The configured <see cref="FinanceManager.Domain.Notifications.HolidayProviderKind"/>.</value>
    public FinanceManager.Domain.Notifications.HolidayProviderKind HolidayProviderKind { get; private set; } = FinanceManager.Domain.Notifications.HolidayProviderKind.Memory;

    /// <summary>
    /// Sets the holiday provider implementation kind used for resolving public holidays.
    /// </summary>
    /// <param name="kind">The provider kind to use.</param>
    public void SetHolidayProvider(FinanceManager.Domain.Notifications.HolidayProviderKind kind)
    {
        HolidayProviderKind = kind;
        Touch();
    }
}

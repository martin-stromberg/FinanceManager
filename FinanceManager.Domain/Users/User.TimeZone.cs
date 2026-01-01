namespace FinanceManager.Domain.Users;

public sealed partial class User
{
    /// <summary>
    /// IANA time zone identifier for the user (e.g. "Europe/Berlin").
    /// </summary>
    /// <value>
    /// The IANA time zone id string, or <c>null</c> if not set.
    /// </value>
    public string? TimeZoneId { get; private set; }

    /// <summary>
    /// Sets the user's IANA time zone identifier. Passing <c>null</c> or whitespace clears the value.
    /// Trims the provided value and validates maximum length.
    /// </summary>
    /// <param name="timeZoneId">The IANA time zone identifier (e.g. "Europe/Berlin"), or <c>null</c> to unset.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeZoneId"/> exceeds 100 characters after trimming.</exception>
    public void SetTimeZoneId(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            TimeZoneId = null; // unset
            Touch();
            return;
        }
        var trimmed = timeZoneId.Trim();
        if (trimmed.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(timeZoneId), "TimeZoneId must be <= 100 characters.");
        }
        TimeZoneId = trimmed;
        Touch();
    }
}

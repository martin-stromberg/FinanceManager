namespace FinanceManager.Domain.Security;

/// <summary>
/// Represents an IP address record that can be blocked/unblocked and tracks failed authentication attempts
/// originating from that IP address.
/// </summary>
public sealed class IpBlock : Entity, IAggregateRoot
{
    /// <summary>
    /// Parameterless constructor for ORM/deserialization.
    /// </summary>
    private IpBlock() { }

    /// <summary>
    /// Creates a new <see cref="IpBlock"/> for the specified IP address.
    /// </summary>
    /// <param name="ipAddress">The IP address to track. Must be non-null and not whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="ipAddress"/> is null or consists only of white-space characters.</exception>
    public IpBlock(string ipAddress)
    {
        IpAddress = Guards.NotNullOrWhiteSpace(ipAddress, nameof(ipAddress)).Trim();
        IsBlocked = false;
    }

    /// <summary>
    /// The tracked IP address.
    /// </summary>
    /// <value>IP address string.</value>
    public string IpAddress { get; private set; } = null!;

    /// <summary>
    /// Whether this IP is currently blocked (blacklisted).
    /// </summary>
    /// <value><c>true</c> when blocked; otherwise <c>false</c>.</value>
    public bool IsBlocked { get; private set; }

    /// <summary>
    /// UTC timestamp when the IP was blocked, or <c>null</c> if not blocked.
    /// </summary>
    /// <value>UTC time of block or null.</value>
    public DateTime? BlockedAtUtc { get; private set; }

    /// <summary>
    /// Optional reason provided when the IP was blocked.
    /// </summary>
    /// <value>The block reason or null.</value>
    public string? BlockReason { get; private set; }

    /// <summary>
    /// Counter for failed attempts that used an unknown or non-existing username from this IP.
    /// </summary>
    /// <value>Number of failed attempts.</value>
    public int UnknownUserFailedAttempts { get; private set; }

    /// <summary>
    /// UTC timestamp of the last failed attempt with an unknown username, or <c>null</c> if none.
    /// </summary>
    /// <value>UTC time of last failed attempt or null.</value>
    public DateTime? UnknownUserLastFailedUtc { get; private set; }

    /// <summary>
    /// Renames (updates) the tracked IP address.
    /// </summary>
    /// <param name="ipAddress">The new IP address. Must be non-null and not whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="ipAddress"/> is null or consists only of white-space characters.</exception>
    public void Rename(string ipAddress)
    {
        IpAddress = Guards.NotNullOrWhiteSpace(ipAddress, nameof(ipAddress)).Trim();
        Touch();
    }

    /// <summary>
    /// Registers a failed authentication attempt that used an unknown username from this IP.
    /// </summary>
    /// <param name="utcNow">The current UTC time when the failure occurred.</param>
    /// <param name="resetAfter">Time span after which the failure counter is reset if no failures occurred.</param>
    /// <returns>The updated number of consecutive unknown-user failed attempts for this IP.</returns>
    /// <remarks>
    /// If the time since the last failure is greater than or equal to <paramref name="resetAfter"/>,
    /// the counter is reset to zero before incrementing.
    /// </remarks>
    public int RegisterUnknownUserFailure(DateTime utcNow, TimeSpan resetAfter)
    {
        if (UnknownUserLastFailedUtc.HasValue && utcNow - UnknownUserLastFailedUtc.Value >= resetAfter)
        {
            UnknownUserFailedAttempts = 0;
        }
        UnknownUserFailedAttempts++;
        UnknownUserLastFailedUtc = utcNow;
        Touch();
        return UnknownUserFailedAttempts;
    }

    /// <summary>
    /// Resets the unknown-user failure counters for this IP.
    /// </summary>
    public void ResetUnknownUserCounters()
    {
        UnknownUserFailedAttempts = 0;
        UnknownUserLastFailedUtc = null;
        Touch();
    }

    /// <summary>
    /// Blocks (blacklists) this IP address.
    /// </summary>
    /// <param name="utcNow">The UTC time when the block is applied.</param>
    /// <param name="reason">Optional reason for blocking; whitespace-only strings are treated as null.</param>
    public void Block(DateTime utcNow, string? reason = null)
    {
        IsBlocked = true;
        BlockedAtUtc = utcNow;
        BlockReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        Touch();
    }

    /// <summary>
    /// Unblocks (removes blacklist) for this IP address and clears block metadata.
    /// </summary>
    public void Unblock()
    {
        IsBlocked = false;
        BlockedAtUtc = null;
        BlockReason = null;
        Touch();
    }
}

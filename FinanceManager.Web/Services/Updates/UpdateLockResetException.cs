namespace FinanceManager.Web.Services.Updates;

/// <summary>
/// Describes the classified reason why an update-lock reset failed.
/// </summary>
public enum UpdateLockResetFailureKind
{
    /// <summary>No active update lock exists.</summary>
    NoLock,

    /// <summary>The active update lock is not old enough to be reset.</summary>
    LockNotStale,

    /// <summary>The active update lock could not be deleted.</summary>
    LockDeleteFailed,

    /// <summary>The reset failed for another technical reason.</summary>
    ResetFailed
}

/// <summary>
/// Describes where a reset failure was detected.
/// </summary>
public enum UpdateLockResetFailureSource
{
    /// <summary>FinanceManager detected the failure from local state or invariants.</summary>
    FinanceManager,

    /// <summary>The updater package store or another updater component surfaced the failure.</summary>
    Updater
}

/// <summary>
/// Carries a classified update-lock reset failure from the update adapter to the API controller.
/// </summary>
public sealed class UpdateLockResetException : IOException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateLockResetException"/> class.
    /// </summary>
    /// <param name="kind">The classified reset failure kind.</param>
    /// <param name="failureSource">Where the failure was detected.</param>
    /// <param name="message">The diagnostic error message.</param>
    /// <param name="lockCreatedAt">The lock creation timestamp, if available.</param>
    /// <param name="lockPath">The lock file path, if available.</param>
    /// <param name="innerException">The underlying technical exception, if available.</param>
    public UpdateLockResetException(
        UpdateLockResetFailureKind kind,
        UpdateLockResetFailureSource failureSource,
        string message,
        DateTimeOffset? lockCreatedAt = null,
        string? lockPath = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        FailureSource = failureSource;
        LockCreatedAt = lockCreatedAt;
        LockPath = lockPath;
    }

    /// <summary>Gets the classified reset failure kind.</summary>
    public UpdateLockResetFailureKind Kind { get; }

    /// <summary>Gets where the failure was detected.</summary>
    public UpdateLockResetFailureSource FailureSource { get; }

    /// <summary>Gets the lock creation timestamp, if available.</summary>
    public DateTimeOffset? LockCreatedAt { get; }

    /// <summary>Gets the lock file path, if available.</summary>
    public string? LockPath { get; }
}

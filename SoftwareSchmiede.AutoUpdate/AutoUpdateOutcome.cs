namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Represents the outcome of a single auto-update operation.
/// </summary>
public enum AutoUpdateOutcome
{
    /// <summary>The operation completed successfully.</summary>
    Success,

    /// <summary>No newer version was available.</summary>
    NoUpdate,

    /// <summary>The operation was skipped because the next automatic step is disabled.</summary>
    Skipped,

    /// <summary>The operation was canceled by an event subscriber.</summary>
    Canceled,

    /// <summary>The operation failed with an error.</summary>
    Failed
}

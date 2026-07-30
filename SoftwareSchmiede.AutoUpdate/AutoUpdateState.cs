namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Represents the lifecycle state of the auto-update subsystem.
/// </summary>
public enum AutoUpdateState
{
    /// <summary>No update activity is currently in progress.</summary>
    Idle,

    /// <summary>The configured source is being queried for a newer version.</summary>
    Checking,

    /// <summary>A newer version was found and is waiting to be downloaded.</summary>
    UpdateAvailable,

    /// <summary>The update package is being downloaded.</summary>
    Downloading,

    /// <summary>The update package has been downloaded and validated and is ready to install.</summary>
    ReadyToInstall,

    /// <summary>The installation script has been started.</summary>
    Installing,

    /// <summary>The installation completed successfully.</summary>
    Success,

    /// <summary>An error occurred during any phase of the update workflow.</summary>
    Failed,

    /// <summary>The auto-update subsystem is disabled via configuration.</summary>
    Disabled
}

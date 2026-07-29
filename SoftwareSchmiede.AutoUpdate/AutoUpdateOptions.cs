namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Runtime-mutable configuration for the auto-update subsystem, registered as a singleton instance by
/// <see cref="AutoUpdateHostBuilderExtensions.UseAutoUpdate"/>.
/// </summary>
public sealed class AutoUpdateOptions
{
    /// <summary>
    /// The minimum allowed value of <see cref="HealthTimeoutSeconds"/>.
    /// </summary>
    public const int MinHealthTimeoutSeconds = 10;

    /// <summary>
    /// The maximum allowed value of <see cref="HealthTimeoutSeconds"/>.
    /// </summary>
    public const int MaxHealthTimeoutSeconds = 600;

    /// <summary>
    /// The default value of <see cref="DownloadPath"/>, also used as the fallback root directory name by
    /// <see cref="FileSystemAutoUpdatePackageStore"/> when <see cref="DownloadPath"/> is blank.
    /// </summary>
    public const string DefaultDownloadPath = "updates";

    /// <summary>
    /// Gets or sets a value indicating whether the auto-update subsystem is enabled at all.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a discovered newer version is downloaded automatically.
    /// </summary>
    public bool EnableAutomaticDownload { get; set; } = true;

    /// <summary>
    /// Gets or sets the root directory update packages, status and lock files are stored in.
    /// </summary>
    public string DownloadPath { get; set; } = DefaultDownloadPath;

    /// <summary>
    /// Gets or sets a value indicating whether a downloaded update package is installed automatically.
    /// </summary>
    public bool EnableAutomaticInstallation { get; set; }

    /// <summary>
    /// Gets or sets the update source used to check for and download new versions.
    /// </summary>
    public IAutoUpdateSource? Source { get; set; }

    /// <summary>
    /// Gets or sets the configuration for the periodic background source check.
    /// </summary>
    public SourceCheckOptions SourceCheck { get; set; } = new();

    /// <summary>
    /// Gets or sets the maximum accepted size, in bytes, of a downloaded update package.
    /// </summary>
    public long MaxAssetBytes { get; set; } = 512L * 1024L * 1024L;

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="AutoUpdateCheckerService"/> and
    /// <see cref="AutoUpdateSchedulerService"/> are registered as hosted services.
    /// </summary>
    public bool HostedServicesEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the time of day scheduled installations should be attempted at, or <see langword="null"/> if
    /// no scheduled installation is configured.
    /// </summary>
    public TimeOnly? ScheduledInstallTime { get; set; }

    /// <summary>
    /// Gets or sets the name of the service to stop and restart during installation.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Gets or sets the path of the executable to restart during installation, used when no service is
    /// configured.
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the host application is stopped after the installation script has
    /// been started.
    /// </summary>
    public bool StopHostAfterScriptStart { get; set; }

    /// <summary>
    /// Gets or sets the number of seconds an installation lock must be older than the health timeout before it
    /// is considered stale and eligible for a manual reset.
    /// </summary>
    public int HealthTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets the systemd unit name <see cref="DefaultAutoUpdateProcessRunner"/> uses on Linux to run the
    /// installation script. Must be unique per consuming application so that concurrently installed applications
    /// using this library do not collide on the same unit.
    /// </summary>
    public string UpdateUnitName { get; set; } = "AutoUpdate";
}

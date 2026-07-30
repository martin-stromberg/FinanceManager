namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Fluent configuration surface for the auto-update subsystem, used from the delegate passed to
/// <see cref="AutoUpdateHostBuilderExtensions.UseAutoUpdate"/>.
/// </summary>
public sealed class AutoUpdateBuilder
{
    /// <summary>
    /// Gets the options instance being configured.
    /// </summary>
    internal AutoUpdateOptions Options { get; } = new();

    /// <summary>
    /// Gets the configuration section name the options are bound from.
    /// </summary>
    internal string ConfigurationSectionName { get; private set; } = AutoUpdateHostBuilderExtensions.DefaultConfigurationSectionName;

    /// <summary>
    /// Gets the download path explicitly set via <see cref="EnableAutomaticDownload"/>, if any. Used by
    /// <see cref="AutoUpdateHostBuilderExtensions.UseAutoUpdate"/> to make sure this explicit value is not
    /// silently overwritten by configuration binding.
    /// </summary>
    internal string? ExplicitDownloadPath { get; private set; }

    /// <summary>
    /// Gets a value indicating whether <see cref="EnableAutomaticDownload"/> was called.
    /// </summary>
    internal bool ExplicitEnableAutomaticDownload { get; private set; }

    /// <summary>
    /// Gets a value indicating whether <see cref="EnableAutomaticInstallation"/> was called.
    /// </summary>
    internal bool ExplicitEnableAutomaticInstallation { get; private set; }

    /// <summary>
    /// Gets the source check interval explicitly set via <see cref="WithSourceCheck"/>, if any.
    /// </summary>
    internal int? ExplicitSourceCheckInterval { get; private set; }

    /// <summary>
    /// Gets the source check time ranges explicitly set via <see cref="WithSourceCheck"/>, if any.
    /// </summary>
    internal IReadOnlyList<SourceCheckTimeRange>? ExplicitSourceCheckTimeRanges { get; private set; }

    /// <summary>
    /// Gets the update unit name explicitly set via <see cref="WithUpdateUnitName"/>, if any.
    /// </summary>
    internal string? ExplicitUpdateUnitName { get; private set; }

    /// <summary>
    /// Enables automatic download of a newer version once discovered.
    /// </summary>
    /// <param name="downloadPath">The root directory update packages are stored in, or <see langword="null"/> to keep the current value.</param>
    /// <returns>The same builder instance, for chaining.</returns>
    public AutoUpdateBuilder EnableAutomaticDownload(string? downloadPath = null)
    {
        Options.EnableAutomaticDownload = true;
        ExplicitEnableAutomaticDownload = true;
        if (!string.IsNullOrWhiteSpace(downloadPath))
        {
            Options.DownloadPath = downloadPath;
            ExplicitDownloadPath = downloadPath;
        }

        return this;
    }

    /// <summary>
    /// Enables automatic installation of a downloaded update package.
    /// </summary>
    /// <returns>The same builder instance, for chaining.</returns>
    public AutoUpdateBuilder EnableAutomaticInstallation()
    {
        Options.EnableAutomaticInstallation = true;
        ExplicitEnableAutomaticInstallation = true;
        return this;
    }

    /// <summary>
    /// Configures a custom update source.
    /// </summary>
    /// <param name="source">The update source to use.</param>
    /// <returns>The same builder instance, for chaining.</returns>
    public AutoUpdateBuilder UseSource(IAutoUpdateSource source)
    {
        Options.Source = source ?? throw new ArgumentNullException(nameof(source));
        return this;
    }

    /// <summary>
    /// Configures a GitHub-releases update source.
    /// </summary>
    /// <param name="repositoryOwner">The owner (user or organization) of the GitHub repository.</param>
    /// <param name="repositoryName">The name of the GitHub repository.</param>
    /// <param name="manifestAssetName">The name of the release manifest asset, or <see langword="null"/> to use <see cref="AutoUpdateGithubSource.DefaultManifestAssetName"/>.</param>
    /// <returns>The same builder instance, for chaining.</returns>
    public AutoUpdateBuilder UseGithubSource(string repositoryOwner, string repositoryName, string? manifestAssetName = null)
    {
        Options.Source = AutoUpdateGithubSource.Create(repositoryOwner, repositoryName, manifestAssetName);
        return this;
    }

    /// <summary>
    /// Configures a local-folder update source.
    /// </summary>
    /// <param name="sourceDirectory">The local directory the release manifest and packages are read from.</param>
    /// <param name="manifestFileName">The name of the release manifest file, or <see langword="null"/> to use <see cref="AutoUpdateLocalFolderSource.DefaultManifestFileName"/>.</param>
    /// <returns>The same builder instance, for chaining.</returns>
    public AutoUpdateBuilder UseLocalFolderSource(string sourceDirectory, string? manifestFileName = null)
    {
        Options.Source = new AutoUpdateLocalFolderSource(sourceDirectory, manifestFileName: manifestFileName);
        return this;
    }

    /// <summary>
    /// Configures the periodic background source check.
    /// </summary>
    /// <param name="interval">The interval, in minutes, between successive checks.</param>
    /// <param name="timeRanges">The allowed time windows, or <see langword="null"/> to allow checks at any time.</param>
    /// <returns>The same builder instance, for chaining.</returns>
    public AutoUpdateBuilder WithSourceCheck(int interval, IEnumerable<SourceCheckTimeRange>? timeRanges = null)
    {
        Options.SourceCheck.Interval = interval;
        ExplicitSourceCheckInterval = interval;
        if (timeRanges is not null)
        {
            var resolvedTimeRanges = timeRanges.ToList();
            Options.SourceCheck.TimeRanges = resolvedTimeRanges;
            ExplicitSourceCheckTimeRanges = resolvedTimeRanges;
        }

        return this;
    }

    /// <summary>
    /// Binds <see cref="AutoUpdateOptions"/> from a configuration section other than the default ("AutoUpdate").
    /// </summary>
    /// <param name="sectionName">The configuration section name to bind from.</param>
    /// <returns>The same builder instance, for chaining.</returns>
    public AutoUpdateBuilder BindConfiguration(string sectionName)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            throw new ArgumentException("Section name must not be empty.", nameof(sectionName));
        }

        ConfigurationSectionName = sectionName;
        return this;
    }

    /// <summary>
    /// Prevents <see cref="AutoUpdateCheckerService"/> and <see cref="AutoUpdateSchedulerService"/> from being
    /// registered as hosted services.
    /// </summary>
    /// <returns>The same builder instance, for chaining.</returns>
    public AutoUpdateBuilder DisableHostedServices()
    {
        Options.HostedServicesEnabled = false;
        return this;
    }

    /// <summary>
    /// Sets the systemd unit name used on Linux to run the installation script (see
    /// <see cref="AutoUpdateOptions.UpdateUnitName"/>). Consuming applications should set a unique value so that
    /// concurrently installed applications using this library do not collide on the same unit.
    /// </summary>
    /// <param name="unitName">The unit name to use.</param>
    /// <returns>The same builder instance, for chaining.</returns>
    public AutoUpdateBuilder WithUpdateUnitName(string unitName)
    {
        if (string.IsNullOrWhiteSpace(unitName))
        {
            throw new ArgumentException("Unit name must not be empty.", nameof(unitName));
        }

        Options.UpdateUnitName = unitName;
        ExplicitUpdateUnitName = unitName;
        return this;
    }

    /// <summary>
    /// Sets the root directory update packages, status and lock files are stored in, without affecting whether
    /// automatic download is enabled. Use <see cref="EnableAutomaticDownload"/> instead to also enable automatic
    /// download of a newer version once discovered.
    /// </summary>
    /// <param name="downloadPath">The root directory update packages are stored in.</param>
    /// <returns>The same builder instance, for chaining.</returns>
    public AutoUpdateBuilder WithDownloadPath(string downloadPath)
    {
        if (string.IsNullOrWhiteSpace(downloadPath))
        {
            throw new ArgumentException("Download path must not be empty.", nameof(downloadPath));
        }

        Options.DownloadPath = downloadPath;
        ExplicitDownloadPath = downloadPath;
        return this;
    }
}

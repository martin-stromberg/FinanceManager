using FinanceManager.Shared.Dtos.Update;
using SoftwareSchmiede.AutoUpdate;

namespace FinanceManager.Web.Services.Updates;

/// <summary>
/// Transfers the runtime-relevant fields of <see cref="UpdateSettingsDto"/> into the auto-update library's
/// singleton <see cref="AutoUpdateOptions"/>, so that changes made through the setup UI take effect immediately.
/// </summary>
public static class AutoUpdateOptionsMapper
{
    /// <summary>
    /// Applies the given settings onto <paramref name="options"/>. If the configured source is a
    /// <see cref="AutoUpdateGithubSource"/>, it is replaced with a new instance reflecting the (possibly changed)
    /// repository owner, repository name and manifest asset name, so that changes made through the setup UI take
    /// effect on the next check instead of only after a restart. The previous source is disposed.
    /// </summary>
    /// <param name="options">The auto-update library's runtime-mutable options to update.</param>
    /// <param name="settings">The settings to apply.</param>
    public static void ApplySettings(AutoUpdateOptions options, UpdateSettingsDto settings)
    {
        options.Enabled = settings.Enabled;
        options.SourceCheck.Interval = Math.Max(1, settings.CheckIntervalMinutes);
        options.ServiceName = settings.ServiceName;
        options.ExecutablePath = settings.ExecutablePath;
        options.DownloadPath = settings.WorkingDirectory;
        options.HealthTimeoutSeconds = settings.HealthTimeoutSeconds;
        options.ScheduledInstallTime = settings.ScheduledInstallTime;

        if (options.Source is AutoUpdateGithubSource previousSource)
        {
            options.Source = AutoUpdateGithubSource.Create(settings.RepositoryOwner, settings.RepositoryName, settings.ManifestAssetName);
            previousSource.Dispose();
        }
    }

    /// <summary>
    /// Builds an <see cref="UpdateSettingsDto"/> from the runtime-relevant fields of <paramref name="options"/>.
    /// The repository owner, repository name and manifest asset name are not part of <see cref="AutoUpdateOptions"/>
    /// (they are FinanceManager-specific and live on <c>UpdateOptions</c>/the persisted settings), so they are
    /// supplied by the caller. The returned values are unnormalized; callers apply their own defaulting/clamping.
    /// </summary>
    /// <param name="options">The auto-update library's runtime-mutable options to read from.</param>
    /// <param name="repositoryOwner">The repository owner to include in the DTO.</param>
    /// <param name="repositoryName">The repository name to include in the DTO.</param>
    /// <param name="manifestAssetName">The manifest asset name to include in the DTO.</param>
    /// <returns>An <see cref="UpdateSettingsDto"/> reflecting the current state of <paramref name="options"/>.</returns>
    public static UpdateSettingsDto ToSettingsDto(AutoUpdateOptions options, string repositoryOwner, string repositoryName, string manifestAssetName)
        => new(
            options.Enabled,
            options.SourceCheck.Interval,
            repositoryOwner,
            repositoryName,
            manifestAssetName,
            options.ScheduledInstallTime,
            options.ServiceName,
            options.ExecutablePath,
            options.DownloadPath,
            options.HealthTimeoutSeconds);
}

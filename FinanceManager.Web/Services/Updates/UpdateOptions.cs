namespace FinanceManager.Web.Services.Updates;

/// <summary>
/// Host-specific binding for the <c>Updates</c> configuration section. Holds the FinanceManager-specific fields
/// (repository, manifest name, source selection) used to build the <see cref="SoftwareSchmiede.AutoUpdate.AutoUpdateBuilder"/>
/// configuration in <c>ProgramExtensions</c>. All runtime-mutable values (automatic download/installation,
/// timeouts, byte limits, hosted services, service/executable targets) are bound only onto
/// <see cref="SoftwareSchmiede.AutoUpdate.AutoUpdateOptions"/> from the same configuration section, not onto this
/// class.
/// </summary>
public sealed class UpdateOptions
{
    /// <summary>
    /// The configuration section name this class is bound from.
    /// </summary>
    public const string SectionName = "Updates";

    /// <summary>
    /// Gets or sets the interval, in minutes, between successive source checks.
    /// </summary>
    public int CheckIntervalMinutes { get; set; } = 360;

    /// <summary>
    /// Gets or sets the owner (user or organization) of the GitHub repository used as the update source.
    /// </summary>
    public string RepositoryOwner { get; set; } = "martin-stromberg";

    /// <summary>
    /// Gets or sets the name of the GitHub repository used as the update source.
    /// </summary>
    public string RepositoryName { get; set; } = "FinanceManager";

    /// <summary>
    /// Gets or sets the file name of the release manifest.
    /// </summary>
    public string ManifestAssetName { get; set; } = "update.json";

    /// <summary>
    /// Gets or sets the root directory update packages, status and lock files are stored in.
    /// </summary>
    public string WorkingDirectory { get; set; } = "updates";

    /// <summary>
    /// Gets or sets which update source implementation is used: <c>Github</c> or <c>LocalFolder</c>.
    /// </summary>
    public string SourceType { get; set; } = "Github";

    /// <summary>
    /// Gets or sets the local directory used by the local-folder source, when <see cref="SourceType"/> is <c>LocalFolder</c>.
    /// </summary>
    public string? LocalFolderPath { get; set; }
}

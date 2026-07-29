namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Describes a release manifest published by an <see cref="IAutoUpdateSource"/>.
/// </summary>
/// <param name="Version">The semantic version of the release.</param>
/// <param name="ReleaseNotes">The release notes, if provided by the source.</param>
/// <param name="PublishedAt">The publication timestamp of the release.</param>
/// <param name="Packages">The packages available for this release, one per supported platform.</param>
/// <returns>An immutable description of a published release manifest.</returns>
public sealed record AutoUpdateReleaseInfo(
    string Version,
    string? ReleaseNotes,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<AutoUpdatePackageDescriptor> Packages);

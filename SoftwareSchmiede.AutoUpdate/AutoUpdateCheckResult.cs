namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Describes the result of querying an <see cref="IAutoUpdateSource"/> for a newer version.
/// </summary>
/// <param name="AvailableVersion">The version reported by the source, or <see langword="null"/> if none was found.</param>
/// <param name="Package">The package descriptor matching the current platform, if a version was found.</param>
/// <param name="ReleaseNotes">The release notes of the available version, if provided.</param>
/// <param name="PublishedAt">The publication timestamp of the available version.</param>
/// <returns>An immutable result describing the outcome of a source check.</returns>
public sealed record AutoUpdateCheckResult(
    string? AvailableVersion,
    AutoUpdatePackageDescriptor? Package,
    string? ReleaseNotes,
    DateTimeOffset? PublishedAt);

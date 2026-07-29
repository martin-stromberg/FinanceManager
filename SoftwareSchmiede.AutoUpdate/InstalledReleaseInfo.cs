namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Describes the currently installed release, as reported by <see cref="IInstalledVersionProvider"/>.
/// </summary>
/// <param name="Version">The installed version, or <see langword="null"/> if unknown.</param>
/// <param name="PublishedAt">The publication timestamp of the installed release.</param>
/// <param name="CommitSha">The source control commit the installed release was built from.</param>
/// <param name="Repository">The repository the installed release originates from.</param>
/// <param name="RuntimeIdentifier">The .NET runtime identifier of the installed release.</param>
/// <returns>An immutable description of the currently installed release.</returns>
public sealed record InstalledReleaseInfo(
    string? Version,
    DateTimeOffset? PublishedAt,
    string? CommitSha,
    string? Repository,
    string? RuntimeIdentifier);

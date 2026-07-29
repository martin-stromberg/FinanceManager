namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Describes the result of starting an update installation.
/// </summary>
/// <param name="Version">The version being installed.</param>
/// <param name="ScriptPath">The local file system path of the generated installation script.</param>
/// <param name="StartedAt">The timestamp the installation script was started at.</param>
/// <returns>An immutable result describing a started installation.</returns>
public sealed record AutoUpdateInstallResult(
    string Version,
    string ScriptPath,
    DateTimeOffset StartedAt);

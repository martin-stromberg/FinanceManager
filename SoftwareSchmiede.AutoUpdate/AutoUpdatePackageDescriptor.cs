namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Describes a single downloadable update package for a specific platform and runtime.
/// </summary>
/// <param name="Version">The semantic version of the package.</param>
/// <param name="Platform">The platform identifier (e.g. "windows", "linux").</param>
/// <param name="RuntimeIdentifier">The .NET runtime identifier (e.g. "win-x64", "linux-x64").</param>
/// <param name="FileName">The file name of the package, without any path segments.</param>
/// <param name="Uri">The location the package can be downloaded from.</param>
/// <param name="Sha256">The expected SHA256 checksum of the downloaded package, in lowercase hex.</param>
/// <param name="SizeBytes">The expected size of the package in bytes.</param>
/// <returns>An immutable descriptor identifying a single downloadable update package.</returns>
public sealed record AutoUpdatePackageDescriptor(
    string Version,
    string Platform,
    string RuntimeIdentifier,
    string FileName,
    Uri Uri,
    string Sha256,
    long SizeBytes);

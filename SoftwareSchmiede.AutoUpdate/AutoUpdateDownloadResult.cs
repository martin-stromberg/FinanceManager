namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Describes the result of downloading an update package.
/// </summary>
/// <param name="LocalPath">The local file system path the package was downloaded to.</param>
/// <param name="SizeBytes">The size of the downloaded file in bytes.</param>
/// <param name="ChecksumValid">Whether the downloaded package's checksum matched the descriptor.</param>
/// <returns>An immutable result describing a completed download.</returns>
public sealed record AutoUpdateDownloadResult(
    string LocalPath,
    long SizeBytes,
    bool ChecksumValid);

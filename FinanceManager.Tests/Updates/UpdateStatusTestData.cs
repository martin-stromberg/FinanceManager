using FinanceManager.Shared.Dtos.Update;

namespace FinanceManager.Tests.Updates;

/// <summary>
/// Shared builder for <see cref="UpdateStatusDto"/> test fixtures, reused across the unit and integration test
/// projects to avoid duplicating the DTO's long positional constructor in multiple places.
/// </summary>
public static class UpdateStatusTestData
{
    /// <summary>
    /// Builds an <see cref="UpdateStatusDto"/> representing an in-progress installation of
    /// <paramref name="availableVersion"/> with an active lock.
    /// </summary>
    /// <param name="availableVersion">The version currently being installed.</param>
    /// <param name="manifest">The manifest metadata to attach, or <see langword="null"/> if none is needed.</param>
    /// <returns>An <see cref="UpdateStatusDto"/> with <see cref="UpdateStatusKind.Installing"/> status.</returns>
    public static UpdateStatusDto InstallingStatus(string availableVersion, UpdateMetadataDto? manifest = null)
        => new(
            UpdateStatusKind.Installing,
            null,
            null,
            availableVersion,
            "win-x64",
            DateTimeOffset.UtcNow,
            null,
            "release.zip",
            true,
            DateTimeOffset.UtcNow,
            null,
            manifest);
}

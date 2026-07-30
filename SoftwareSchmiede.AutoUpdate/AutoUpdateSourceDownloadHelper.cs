namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Shared download-to-disk logic for <see cref="IAutoUpdateSource"/> implementations: ensures the target
/// directory exists, streams into a temporary file while enforcing a size limit, and atomically moves the
/// temporary file onto the target path only once fully written - so a canceled or failed download never leaves a
/// partial file under the final name.
/// </summary>
internal static class AutoUpdateSourceDownloadHelper
{
    /// <summary>
    /// Copies <paramref name="source"/> to <paramref name="targetPath"/> via a temporary file and an atomic move.
    /// </summary>
    /// <param name="source">The stream to read the package content from.</param>
    /// <param name="targetPath">The final path the package should be stored at.</param>
    /// <param name="maxBytes">The maximum accepted size, in bytes.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <exception cref="InvalidOperationException">Thrown when the copied content exceeds <paramref name="maxBytes"/>.</exception>
    public static async Task CopyToTargetAsync(Stream source, string targetPath, long maxBytes, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var tempPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var target = File.Create(tempPath))
            {
                var buffer = new byte[81920];
                long copied = 0;
                while (true)
                {
                    var read = await source.ReadAsync(buffer, ct);
                    if (read == 0)
                    {
                        break;
                    }

                    copied += read;
                    if (copied > maxBytes)
                    {
                        throw new InvalidOperationException("Update package exceeds the configured size limit.");
                    }

                    await target.WriteAsync(buffer.AsMemory(0, read), ct);
                }
            }

            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // Best-effort cleanup only; the original exception must propagate.
            }

            throw;
        }
    }
}

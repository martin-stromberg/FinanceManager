using System.Text.Json;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Atomic JSON file read/write helper shared by the library's file-based stores and available to consuming
/// applications for their own auto-update-adjacent settings files.
/// </summary>
public static class JsonFileStore
{
    /// <summary>
    /// The <see cref="JsonSerializerOptions"/> used for all reads and writes performed by this class.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    /// <summary>
    /// Reads and deserializes the JSON file at <paramref name="path"/>, or returns <see langword="default"/> if it
    /// does not exist.
    /// </summary>
    /// <typeparam name="T">The type to deserialize into.</typeparam>
    /// <param name="path">The full path of the JSON file to read.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>The deserialized value, or <see langword="default"/> if the file does not exist.</returns>
    public static async Task<T?> ReadAsync<T>(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }

    /// <summary>
    /// Serializes <paramref name="value"/> to a temporary file next to <paramref name="path"/> and atomically
    /// moves it into place, so readers never observe a partially written file.
    /// </summary>
    /// <typeparam name="T">The type to serialize.</typeparam>
    /// <param name="path">The full path of the JSON file to write.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>A task that completes once the file has been written.</returns>
    public static async Task WriteAtomicAsync<T>(string path, T value, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, JsonOptions, ct);
        }

        File.Move(tempPath, path, overwrite: true);
    }
}

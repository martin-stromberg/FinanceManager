namespace FinanceManager.Shared.Dtos.Reports;

/// <summary>
/// Shared helpers for the free text filter lists used by report favorites and report
/// aggregation queries. Centralizes normalization (trim, drop empty, case-insensitive
/// dedupe), JSON persistence and the maximum entry length so that all layers apply
/// identical rules.
/// </summary>
public static class TextFilterList
{
    /// <summary>
    /// Maximum number of characters allowed per single text filter entry.
    /// </summary>
    public const int MaxEntryLength = 500;

    /// <summary>
    /// Normalizes raw text filter input: trims each entry, drops entries that are empty
    /// after trimming and deduplicates case-insensitively
    /// (<see cref="StringComparer.OrdinalIgnoreCase"/>).
    /// </summary>
    /// <param name="patterns">Raw text filter entries; may be <c>null</c>.</param>
    /// <returns>The normalized entries, or <c>null</c> when nothing remains.</returns>
    public static IReadOnlyCollection<string>? Normalize(IEnumerable<string?>? patterns)
    {
        if (patterns == null) { return null; }
        var normalized = patterns
            .Select(p => p?.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalized.Length == 0 ? null : normalized;
    }

    /// <summary>
    /// Serializes normalized text filter entries to a JSON string array.
    /// </summary>
    /// <param name="patterns">Raw text filter entries; may be <c>null</c>.</param>
    /// <returns>A JSON string array, or <c>null</c> when the input normalizes to nothing.</returns>
    /// <exception cref="ArgumentException">Thrown when an entry exceeds <see cref="MaxEntryLength"/> characters.</exception>
    public static string? ToJson(IEnumerable<string?>? patterns)
    {
        var normalized = Normalize(patterns);
        if (normalized == null) { return null; }
        foreach (var entry in normalized)
        {
            if (entry.Length > MaxEntryLength)
            {
                throw new ArgumentException($"Text filter entries must not exceed {MaxEntryLength} characters.", nameof(patterns));
            }
        }
        return System.Text.Json.JsonSerializer.Serialize(normalized.ToArray());
    }

    /// <summary>
    /// Parses a JSON string array produced by <see cref="ToJson"/> back into a list of entries.
    /// Malformed payloads (e.g. manipulated or corrupted backup data) are tolerated and
    /// yield <c>null</c> instead of throwing, so a damaged stored value cannot break every
    /// subsequent read of the owning entity.
    /// </summary>
    /// <param name="json">JSON serialized string array; may be <c>null</c>.</param>
    /// <returns>The parsed entries, or <c>null</c> when the input is empty, malformed or yields no entries.</returns>
    public static IReadOnlyCollection<string>? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) { return null; }
        try
        {
            var items = System.Text.Json.JsonSerializer.Deserialize<string[]>(json);
            return items is { Length: > 0 } ? items : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}

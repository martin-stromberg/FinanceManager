using FinanceManager.Application.Reports;
using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace FinanceManager.Web.Infrastructure;

/// <summary>
/// Shared helpers for export endpoints: format parsing, file name sanitization and the configured row limit.
/// </summary>
public static class ExportRequestHelper
{
    /// <summary>
    /// Parses the requested export format. A null, empty or whitespace value defaults to <see cref="PostingExportFormat.Csv"/>.
    /// </summary>
    /// <param name="format">Format query value supplied by the caller.</param>
    /// <param name="result">The parsed export format when parsing succeeds.</param>
    /// <returns><see langword="true"/> when the value is empty or a supported format; otherwise <see langword="false"/>.</returns>
    public static bool TryParseFormat(string? format, out PostingExportFormat result)
    {
        result = PostingExportFormat.Csv;
        if (string.IsNullOrWhiteSpace(format)) { return true; }
        var f = format.Trim().ToLowerInvariant();
        if (f == "csv") { result = PostingExportFormat.Csv; return true; }
        if (f == "xlsx") { result = PostingExportFormat.Xlsx; return true; }
        return false;
    }

    /// <summary>
    /// Sanitizes a display name for use inside a download file name: invalid file name characters and spaces
    /// are replaced by underscores and the result is truncated to 60 characters.
    /// </summary>
    /// <param name="value">Input display name.</param>
    /// <returns>A file-name-safe string; "_" when the sanitized input would be empty.</returns>
    public static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        if (cleaned.Length > 60) { cleaned = cleaned.Substring(0, 60); }
        if (string.IsNullOrWhiteSpace(cleaned)) { cleaned = "_"; }
        return cleaned.Replace(' ', '_');
    }

    /// <summary>
    /// Reads the configured maximum export row count from the "Exports:MaxRows" configuration key,
    /// falling back to the provided default when unset or unparsable. The result is at least 1.
    /// </summary>
    /// <param name="config">Application configuration.</param>
    /// <param name="defaultMaxRows">Fallback row limit used when the configuration value is missing or invalid.</param>
    /// <returns>The effective maximum number of export rows.</returns>
    public static int GetMaxRows(IConfiguration config, int defaultMaxRows)
    {
        var maxRowsStr = config["Exports:MaxRows"];
        var maxRows = defaultMaxRows;
        if (!string.IsNullOrWhiteSpace(maxRowsStr) && int.TryParse(maxRowsStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cfgMax))
        {
            maxRows = Math.Max(1, cfgMax);
        }
        return maxRows;
    }
}

using Microsoft.Extensions.Logging;
using System.Text;
using System.Linq;

namespace FinanceManager.Infrastructure.Statements.Files
{
    /// <summary>
    /// Provides functionality to read the contents of a text-based statement file and return its lines as strings.
    /// </summary>
    /// <remarks>This class reads the file content using UTF-8 encoding and splits it into lines based on
    /// standard newline characters. It is intended for use with CSV files where each line represents a record or
    /// statement. Inherits from <see cref="BaseStatementFile"/>.</remarks>
    public class TextStatementFile : BaseStatementFile
    {
        /// <summary>
        /// Initializes a new instance of the TextStatementFile class with the specified logger.
        /// </summary>
        /// <param name="logger">The logger to use for recording diagnostic and operational messages. Cannot be null.</param>
        public TextStatementFile(ILogger logger) : base(logger)
        {
        }

        /// <summary>
        /// Detected encoding used to decode the loaded file. Defaults to UTF8 if detection wasn't performed yet.
        /// </summary>
        public Encoding DetectedEncoding { get; private set; } = Encoding.UTF8;

        /// <summary>
        /// Attempts to load the specified file as a text-based file, validating that the provided bytes represent a
        /// supported text encoding.
        /// </summary>
        /// <remarks>This method checks for common text file encodings, including UTF-8 and UTF-16 with or
        /// without a byte order mark (BOM), and rejects files containing embedded NUL bytes or invalid UTF-8 sequences.
        /// Files that do not meet these criteria are not loaded as text and the method returns false. The method will
        /// additionally attempt several legacy code pages (e.g. Windows-1252) and choose the first encoding that
        /// round-trips the bytes without loss.</remarks>
        /// <param name="fileName">The original filename of the statement file (used for logging or metadata). May be null or empty.</param>
        /// <param name="fileBytes">The contents of the file to load, as a byte array. Must not be null or empty.</param>
        /// <returns>true if the file is recognized as a supported text format and loaded successfully; otherwise, false.</returns>
        public override bool Load(string fileName, byte[] fileBytes)
        {
            if (!base.Load(fileName, fileBytes))
                return false;

            if (fileBytes == null || fileBytes.Length == 0)
                return false;

            Logger?.LogInformation("Loading text file {FileName} ({Size} bytes)", fileName ?? "<null>", fileBytes.Length);

            // Quick heuristic: presence of embedded NUL bytes strongly indicates a binary file
            if (fileBytes.Any(b => b == 0))
            {
                Logger?.LogDebug("File {FileName} contains embedded NUL bytes -> treat as binary", fileName ?? "<null>");
                return false;
            }

            // Check for common BOMs (UTF-8, UTF-16 LE/BE) -> select corresponding encoding
            if (fileBytes.Length >= 3 && fileBytes[0] == 0xEF && fileBytes[1] == 0xBB && fileBytes[2] == 0xBF)
            {
                DetectedEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
                Logger?.LogDebug("Detected UTF-8 BOM for file {FileName}", fileName ?? "<null>");
                return true; // UTF-8 BOM
            }
            if (fileBytes.Length >= 2 && fileBytes[0] == 0xFF && fileBytes[1] == 0xFE)
            {
                DetectedEncoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
                Logger?.LogDebug("Detected UTF-16 LE BOM for file {FileName}", fileName ?? "<null>");
                return true; // UTF-16 LE BOM
            }
            if (fileBytes.Length >= 2 && fileBytes[0] == 0xFE && fileBytes[1] == 0xFF)
            {
                DetectedEncoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
                Logger?.LogDebug("Detected UTF-16 BE BOM for file {FileName}", fileName ?? "<null}");
                return true; // UTF-16 BE BOM
            }

            // Attempt strict UTF-8 decode first
            try
            {
                var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                utf8Strict.GetString(fileBytes);
                DetectedEncoding = Encoding.UTF8;
                Logger?.LogDebug("File {FileName} decoded successfully as strict UTF-8", fileName ?? "<null>");
                return true;
            }
            catch (Exception ex)
            {
                Logger?.LogDebug(ex, "Strict UTF-8 decode failed for file {FileName}", fileName ?? "<null>");
                // Not valid UTF-8; try legacy single-byte encodings via round-trip check
            }

            // Register code page provider to allow access to legacy encodings on .NET Core
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var candidateCodePages = new[] { 1252, 850, 28591, 437 };
            foreach (var cp in candidateCodePages)
            {
                try
                {
                    Logger?.LogDebug("Trying code page {CodePage} for file {FileName}", cp, fileName ?? "<null>");
                    var enc = Encoding.GetEncoding(cp);
                    string decoded = enc.GetString(fileBytes);
                    var round = enc.GetBytes(decoded);
                    if (round.SequenceEqual(fileBytes))
                    {
                        DetectedEncoding = enc;
                        Logger?.LogInformation("Selected code page {CodePage} ({EncodingName}) for file {FileName}", cp, enc.WebName, fileName ?? "<null>");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogDebug(ex, "Code page {CodePage} not suitable for file {FileName}", cp, fileName ?? "<null>");
                    // ignore unsupported code page or decoding errors and try next
                }
            }

            // As a last resort, attempt a permissive UTF-8 decode (will replace invalid bytes) but mark as detected
            // only if resulting string contains printable characters; this is conservative and avoids silent acceptance
            try
            {
                var permissiveUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
                var s = permissiveUtf8.GetString(fileBytes);
                if (!string.IsNullOrWhiteSpace(s))
                {
                    DetectedEncoding = permissiveUtf8;
                    Logger?.LogInformation("Falling back to permissive UTF-8 for file {FileName}", fileName ?? "<null>");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger?.LogDebug(ex, "Permissive UTF-8 decode failed for file {FileName}", fileName ?? "<null>");
                // fall through
            }

            Logger?.LogWarning("Failed to detect text encoding for file {FileName}", fileName ?? "<null>");
            return false;
        }
        /// <summary>
        /// Reads the content of a text file using the previously detected encoding and returns its lines as a sequence of
        /// strings.
        /// </summary>
        /// <remarks>Line breaks are recognized as either '\r\n' or '\n'. The method does not trim
        /// whitespace from lines or filter out empty lines.</remarks>
        /// <returns>An enumerable collection of strings, each representing a line from the decoded file content. The collection
        /// will be empty if the file contains no lines.</returns>
        public override IEnumerable<string> ReadContent()
        {
            string content;
            try
            {
                Logger?.LogDebug("Decoding file content using encoding {Encoding}", DetectedEncoding?.WebName ?? "<null>");
                content = DetectedEncoding.GetString(FileBytes);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to decode file content using encoding {Encoding}", DetectedEncoding?.WebName ?? "<null>");
                throw;
            }

            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            Logger?.LogInformation("Decoded content into {LineCount} lines using {Encoding}", lines.Length, DetectedEncoding?.WebName ?? "<null>");
            foreach (var line in lines)
            {
                yield return line;
            }
        }
    }
}

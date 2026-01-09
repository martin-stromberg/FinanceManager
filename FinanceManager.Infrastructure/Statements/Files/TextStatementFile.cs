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
        /// Attempts to load the specified file as a text-based file, validating that the provided bytes represent a
        /// supported text encoding.
        /// </summary>
        /// <remarks>This method checks for common text file encodings, including UTF-8 and UTF-16 with or
        /// without a byte order mark (BOM), and rejects files containing embedded NUL bytes or invalid UTF-8 sequences.
        /// Files that do not meet these criteria are not loaded as text and the method returns false.</remarks>
        /// <param name="fileName">The original filename of the statement file (used for logging or metadata). May be null or empty.</param>
        /// <param name="fileBytes">The contents of the file to load, as a byte array. Must not be null or empty.</param>
        /// <returns>true if the file is recognized as a supported text format and loaded successfully; otherwise, false.</returns>
        public override bool Load(string fileName, byte[] fileBytes)
        {
            if (!base.Load(fileName, fileBytes))
                return false;

            if (fileBytes == null || fileBytes.Length == 0)
                return false;

            // Quick heuristic: presence of embedded NUL bytes strongly indicates a binary file
            if (fileBytes.Any(b => b == 0))
                return false;

            // Check for common BOMs (UTF-8, UTF-16 LE/BE) -> treat as text
            if (fileBytes.Length >= 3 && fileBytes[0] == 0xEF && fileBytes[1] == 0xBB && fileBytes[2] == 0xBF)
                return true; // UTF-8 BOM
            if (fileBytes.Length >= 2 && fileBytes[0] == 0xFF && fileBytes[1] == 0xFE)
                return true; // UTF-16 LE BOM
            if (fileBytes.Length >= 2 && fileBytes[0] == 0xFE && fileBytes[1] == 0xFF)
                return true; // UTF-16 BE BOM

            // Attempt strict UTF-8 decode: if invalid sequences are present this will throw
            try
            {
                var utf8Strict = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                utf8Strict.GetString(fileBytes);
                return true;
            }
            catch
            {
                // Not valid UTF-8; consider it non-text for CSV reader purposes
                return false;
            }
        }
        /// <summary>
        /// Reads the content of a UTF-8 encoded text file from a byte array and returns its lines as a sequence of
        /// strings.
        /// </summary>
        /// <remarks>Line breaks are recognized as either '\r\n' or '\n'. The method does not trim
        /// whitespace from lines or filter out empty lines.</remarks>
        /// <returns>An enumerable collection of strings, each representing a line from the decoded file content. The collection
        /// will be empty if the file contains no lines.</returns>
        public override IEnumerable<string> ReadContent()
        {
            var content = System.Text.Encoding.UTF8.GetString(FileBytes);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                yield return line;
            }
        }
    }
}

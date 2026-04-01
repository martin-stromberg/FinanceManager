using Microsoft.Extensions.Logging;
using System.Linq;

namespace FinanceManager.Infrastructure.Statements.Files
{
    /// <summary>
    /// Represents a PDF statement file specific to Sparkasse; initially a simple PdfStatementFile wrapper.
    /// </summary>
    public class Sparkasse_PDF_StatementFile : PdfStatementFile
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public Sparkasse_PDF_StatementFile() : base(null) { }

        /// <summary>
        /// Creates a new instance using the provided logger.
        /// </summary>
        /// <param name="logger">Logger to pass to the base PdfStatementFile.</param>
        public Sparkasse_PDF_StatementFile(ILogger<Sparkasse_PDF_StatementFile> logger) : base(logger)
        {
            // default settings can be adjusted later
        }

        /// <summary>
        /// Loads the file and uses a heuristic to verify it's a Sparkasse PDF.
        /// Detection: requires at least two lines that contain only hyphens ('-') and any line
        /// before the first such separator line must start with "Sparkasse" (case-insensitive).
        /// </summary>
        /// <param name="fileName">Original filename.</param>
        /// <param name="fileBytes">File contents.</param>
        /// <returns>True when the file was loaded and matches the Sparkasse heuristic.</returns>
        public override bool Load(string fileName, byte[] fileBytes)
        {
            var currentParsingMode = ParsingMode;
            ParsingMode = LineParsingMode.TextAndTables;
            if (!base.Load(fileName, fileBytes))
                return false;

            var content = ReadContent().ToList();
            if (!content.Any())
            {
                return false;
            }

            // Find all indices where the trimmed line consists only of '-' characters and has length > 0
            var dashLineIndices = content
                .Select((line, index) => new { line, index })
                .Where(x => !string.IsNullOrWhiteSpace(x.line) && x.line.Trim().All(c => c == '-'))
                .Select(x => x.index)
                .ToList();

            var ok = false;
            if (dashLineIndices.Count >= 2)
            {
                var firstDashIndex = dashLineIndices.Min();
                // Check any line before the first dash line for a Sparkasse header
                if (firstDashIndex > 0)
                {
                    for (int i = 0; i < firstDashIndex; i++)
                    {
                        var candidate = content[i]?.TrimStart() ?? string.Empty;
                        if (candidate.StartsWith("Sparkasse", System.StringComparison.OrdinalIgnoreCase))
                        {
                            ok = true;
                            break;
                        }
                    }
                }
            }

            if (!ok)
                return false;

            if (ParsingMode != currentParsingMode)
            {
                ParsingMode = currentParsingMode;
                return base.Load(fileName, fileBytes);
            }

            return true;
        }
    }
}

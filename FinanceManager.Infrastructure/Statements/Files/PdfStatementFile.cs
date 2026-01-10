using FinanceManager.Infrastructure.Statements.Parsers;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;

namespace FinanceManager.Infrastructure.Statements.Files
{
    /// <summary>
    /// Provides functionality to read and extract structured text content from PDF statement files.
    /// </summary>
    /// <remarks>This class parses PDF files and returns their content as a sequence of text lines, with table
    /// rows represented as pipe-separated values. It is intended for use with statement documents where tabular and
    /// textual data need to be extracted for further processing. Inherits from BaseStatementFileReader and specializes
    /// content extraction for PDF format.</remarks>
    public class PdfStatementFile : BaseStatementFile
    {
        /// <summary>
        /// Initializes a new instance of the PdfStatementFile class with the specified logger.
        /// </summary>
        /// <param name="logger">The logger instance used to record diagnostic and operational messages for this file.</param>
        public PdfStatementFile(ILogger logger) : base(logger)
        {
        }

        /// <summary>
        /// Specifies the available modes for parsing lines, determining whether text, tables, or both are processed.
        /// </summary>
        /// <remarks>Use this enumeration to control how line content is interpreted during parsing
        /// operations. Selecting the appropriate mode allows callers to focus on text, tables, or a combination,
        /// depending on the requirements of the parsing scenario.</remarks>
        public enum LineParsingMode {
            /// <summary>
            /// Specifies that only text content is allowed, excluding any formatting or markup.
            /// </summary>
            TextOnly,
            /// <summary>
            /// Specifies that only table structured lines are included in the operation.
            /// </summary>
            TablesOnly,
            /// <summary>
            /// Represents a container for text content and associated tables.
            /// </summary>
            TextAndTables
        }

        /// <summary>
        /// Gets or sets the mode used to parse lines, determining how text and table data are interpreted.
        /// </summary>
        /// <remarks>Use this property to specify whether lines should be parsed as plain text, tables, or
        /// both. The selected mode affects how input data is processed and which parsing rules are applied.</remarks>
        public LineParsingMode ParsingMode { get; set; } = LineParsingMode.TextAndTables;
        /// <summary>
        /// Gets or sets the minimum number of spaces to use when separating table columns.
        /// </summary>
        public int MinTableColumnSpaceSize { get; set; } = 2;

        /// <summary>
        /// Attempts to load a PDF document from the specified byte array and verifies that the data represents a valid
        /// PDF file.
        /// </summary>
        /// <remarks>This method performs basic validation to ensure the file is a PDF, including checking
        /// for the PDF file signature and attempting to open the document. If the file is invalid or corrupted, the
        /// method returns false.</remarks>
        /// <param name="fileName">The original filename of the statement file (used for logging or metadata). May be null or empty.</param>
        /// <param name="fileBytes">The byte array containing the contents of the file to load. Must not be null or empty.</param>
        /// <returns>true if the byte array represents a valid PDF document; otherwise, false.</returns>
        public override bool Load(string fileName, byte[] fileBytes)
        {
            // store bytes first
            if (!base.Load(fileName, fileBytes))
                return false;

            if (fileBytes == null || fileBytes.Length == 0)
                return false;

            Logger?.LogInformation("Loading PDF file {FileName} ({Size} bytes)", fileName ?? "<null>", fileBytes.Length);

            // Quick check: PDF files start with ASCII "%PDF-" - but some PDFs may contain
            // leading garbage or BOM-like bytes before the signature. Search the first
            // part of the file for the signature instead of requiring it at index 0.
            if (fileBytes.Length < 5)
                return false;

            var signature = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }; // "%PDF-"
            bool foundSignature = false;
            int searchLen = (int)Math.Min(20, fileBytes.Length - signature.Length + 1);
            for (int i = 0; i < searchLen; i++)
            {
                bool match = true;
                for (int j = 0; j < signature.Length; j++)
                {
                    if (fileBytes[i + j] != signature[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    foundSignature = true;
                    break;
                }
            }

            if (!foundSignature)
            {
                Logger?.LogDebug("PDF signature '%PDF-' not found in first {SearchLen} bytes", searchLen);
                return false;
            }

            // Check for EOF marker near the end (some PDFs may be appended with incremental updates)
            try
            {
                var tailLen = Math.Min(4096, fileBytes.Length);
                var tail = Encoding.ASCII.GetString(fileBytes, fileBytes.Length - tailLen, tailLen);
                if (!tail.Contains("%%EOF"))
                {
                    // not strictly required but helpful to filter corrupted/non-pdf blobs
                    // still try to open below
                    Logger?.LogDebug("PDF EOF marker '%%EOF' not found near file end");
                }
            }
            catch (Exception ex)
            {
                Logger?.LogDebug(ex, "Error while scanning PDF tail for EOF marker");
                // ignore decoding errors and continue to attempt opening
            }

            // Finally, attempt to open with PdfPig to validate structure
            try
            {
                using (var doc = UglyToad.PdfPig.PdfDocument.Open(fileBytes))
                {
                    // if open succeeds, treat as PDF
                    Logger?.LogInformation("PDF validated successfully by PdfPig");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "PdfPig failed to open PDF file {FileName}", fileName ?? "<null>");
                return false;
            }
        }
        /// <summary>
        /// Extracts lines of text and table rows from the specified PDF file content, returning each non-empty line as
        /// a string.
        /// </summary>
        /// <remarks>The extraction respects the current parsing mode, which determines whether text,
        /// tables, or both are included. Lines are separated based on their position and orientation within the PDF.
        /// Table rows are returned as pipe-delimited strings when applicable.</remarks>
        /// <returns>An enumerable collection of strings, each representing a non-empty line or table row extracted from the PDF.
        /// The collection will be empty if no content is found.</returns>
        public override IEnumerable<string> ReadContent()
        {
            var sb = new StringBuilder();
            using (var doc = UglyToad.PdfPig.PdfDocument.Open(FileBytes))
            {
                var pages = doc.GetPages().ToList();
                Logger?.LogInformation("Opened PDF document with {PageCount} pages", pages.Count);

                int pageIndex = 0;
                foreach (var page in pages)
                {
                    pageIndex++;
                    var words = page.GetWords().ToList();
                    // determine maximum right edge on page to allow right-aligning last column
                    Logger?.LogDebug("Processing page {PageIndex}: {WordCount} words", pageIndex, words.Count);
                    var pageMaxRight = words.Any() ? words.Max(w => w.BoundingBox.Right) : 0.0;
                    // determine page-wide left margin and a stable estimated char width (median over all words)
                    var pageMinLeft = words.Any() ? words.Min(w => w.BoundingBox.Left) : 0.0;
                    var pageCharWidthEstimates = words
                        .Where(w => !string.IsNullOrEmpty(w.Text))
                        .Select(w => w.BoundingBox.Width / Math.Max(1, (double)w.Text.Length))
                        .ToList();
                    double pageCharWidth = 3.0;
                    if (pageCharWidthEstimates.Count > 0)
                    {
                        pageCharWidthEstimates.Sort();
                        pageCharWidth = pageCharWidthEstimates[pageCharWidthEstimates.Count / 2];
                        if (pageCharWidth <= 0) pageCharWidth = 3.0;
                    }

                    // 1. Zeilen erkennen
                    var comparer = new DoubleToleranceComparer(3); // etwas enger
                    // Grouping by equality comparer with a tolerance is not transitive and can produce
                    // surprising results when used with hashing-based grouping. Instead cluster words
                    // deterministically by their Top coordinate in descending order using a sequential
                    // proximity check.
                    double topTolerance = 3.0;
                    var wordsByTopDesc = words.OrderByDescending(w => w.BoundingBox.Top).ToList();
                    var lineGroups = new List<List<Word>>();
                    foreach (var w in wordsByTopDesc)
                    {
                        if (lineGroups.Count == 0)
                        {
                            lineGroups.Add(new List<Word> { w });
                            continue;
                        }

                        var current = lineGroups.Last();
                        // use the average top of the current group as representative to avoid
                        // sensitivity to the first item's exact coordinate
                        double repTop = current.Average(cw => cw.BoundingBox.Top);
                        if (Math.Abs(w.BoundingBox.Top - repTop) <= topTolerance)
                        {
                            current.Add(w);
                        }
                        else
                        {
                            lineGroups.Add(new List<Word> { w });
                        }
                    }

                    // convert to the same shape expected later: list of groups ordered top->bottom
                    var lines = lineGroups.Select(g => (Key: g[0].BoundingBox.Top, Group: g.AsEnumerable())).ToList();


                    // Precompute sorted horizontal words per line to allow multi-line analysis
                    var sortedLines = lines
                        .Select(g => g.Group.Where(w => w.TextOrientation == TextOrientation.Horizontal).OrderBy(w => w.BoundingBox.Left).ToList())
                        .ToList();


                    // Build raw lines for the page to allow space-based column detection
                    var pageLineInfos = new List<(List<Word> Words, string Raw)>();
                    foreach (var sortedWords in sortedLines)
                    {
                        if (sortedWords.Count == 0)
                            continue;
                        var rawLine = GetRawLine(pageMaxRight, sortedWords, pageMinLeft, pageCharWidth);
                        pageLineInfos.Add((sortedWords, rawLine));
                    }

                    // Merge isolated punctuation-only lines (e.g. hyphen placed on its own line due to coordinate differences)
                    // into an adjacent line if vertically close. This fixes cases like "Sparplan Allgemein - Monatsueberschuss"
                    // where the dash gets grouped on a separate line.
                    int mergedPunctuation = 0;
                    for (int idx = pageLineInfos.Count - 1; idx >= 0; idx--)
                    {
                        var (lineWords, raw) = pageLineInfos[idx];
                        if (lineWords.Count == 1)
                        {
                            var w = lineWords[0];
                            var txt = w.Text?.Trim() ?? string.Empty;
                            if (!string.IsNullOrEmpty(txt) && Regex.IsMatch(txt, "^[\\p{P}\\-–—]+$"))
                            {
                                int neighborIdx = -1;
                                if (idx - 1 >= 0) neighborIdx = idx - 1;
                                else if (idx + 1 < pageLineInfos.Count) neighborIdx = idx + 1;

                                if (neighborIdx >= 0)
                                {
                                    var neighborWords = pageLineInfos[neighborIdx].Words;
                                    double hyphenTop = w.BoundingBox.Top;
                                    double neighborTop = neighborWords.Count > 0 ? neighborWords[0].BoundingBox.Top : hyphenTop;
                                    // allow a slightly larger tolerance for these edge cases
                                    if (Math.Abs(hyphenTop - neighborTop) <= 6.0)
                                    {
                                        int insertAt = neighborWords.FindIndex(nw => nw.BoundingBox.Left > w.BoundingBox.Left);
                                        if (insertAt < 0) insertAt = neighborWords.Count;
                                        neighborWords.Insert(insertAt, w);

                                        var newRaw = GetRawLine(pageMaxRight, neighborWords, pageMinLeft, pageCharWidth);
                                        pageLineInfos[neighborIdx] = (neighborWords, newRaw);

                                        pageLineInfos.RemoveAt(idx);
                                        mergedPunctuation++;
                                    }
                                }
                            }
                        }
                    }

                    if (mergedPunctuation > 0)
                        Logger?.LogDebug("Merged {MergedCount} isolated punctuation tokens on page {PageIndex}", mergedPunctuation, pageIndex);

                    // Now process lines using simple per-line space-based detection:
                    // - treat the generated raw line (position-aware with spaces) independently
                    // - split on two-or-more consecutive spaces to get fields
                    foreach (var li in pageLineInfos)
                    {
                        var raw = li.Raw ?? string.Empty;
                        if (ParsingMode == LineParsingMode.TextOnly)
                        {
                            sb.AppendLine(Regex.Replace(raw.Trim(), "\\s+", " "));
                            continue;
                        }

                        // split on N+ spaces => field separator (configurable via MinTableColumnSpaceSize)
                        var splitPattern = $@"\s{{{MinTableColumnSpaceSize},}}"; // e.g. "\s{2,}"
                        var fields = Regex.Split(raw.Trim(), splitPattern)
                                          .Select(f => Regex.Replace(f, "\\s+", " ").Trim())
                                          .Where(f => !string.IsNullOrWhiteSpace(f))
                                          .ToArray();

                        if (fields.Length > 1)
                        {
                            if (ParsingMode == LineParsingMode.TablesOnly || ParsingMode == LineParsingMode.TextAndTables)
                                sb.AppendLine(string.Join("|", fields));
                        }
                        else if (ParsingMode == LineParsingMode.TextAndTables)
                        {
                            sb.AppendLine(fields.Length == 1 ? fields[0] : string.Empty);
                        }
                    }
                    sb.AppendLine();
                }
            }

            // 3. Leere Zeilen entfernen
            string fullText = sb.ToString();
            var pageLines = fullText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in pageLines.Where(line => !string.IsNullOrWhiteSpace(line)))
                yield return line;
        }

        private static string GetRawLine(double pageMaxRight, List<Word> sortedWords, double pageMinLeft, double charWidth)
        {
            // Map each word's left position to an absolute character column index on the page
            int RightTargetCol = Math.Max(0, (int)Math.Round((pageMaxRight - pageMinLeft) / charWidth));

            var lineSb = new StringBuilder();

            for (int wIdx = 0; wIdx < sortedWords.Count; wIdx++)
            {
                var w = sortedWords[wIdx];
                var text = w.Text ?? string.Empty;

                // desired start column for this word relative to pageMinLeft
                int targetCol = Math.Max(0, (int)Math.Round((w.BoundingBox.Left - pageMinLeft) / charWidth));

                // ensure at least one separator between consecutive words if rounding causes overlap
                int neededSpaces = targetCol - lineSb.Length;
                if (neededSpaces <= 0)
                    neededSpaces = 1;

                lineSb.Append(' ', neededSpaces);
                lineSb.Append(text);
            }

            // pad to right edge so last column is right-aligned
            int padAfter = RightTargetCol - lineSb.Length;
            if (padAfter <= 0)
                padAfter = 1;
            lineSb.Append(' ', padAfter);

            return lineSb.ToString();
        }

        // ClusterColumns removed - using simple per-line whitespace splitting for column detection

    }
 
}

using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Statements.Files
{
    /// <summary>
    /// Represents a PDF statement file specific to Barclays bank, providing parsing logic tailored to Barclays
    /// statement formats.
    /// </summary>
    /// <remarks>Use this class to process and extract data from Barclays PDF bank statements. Inherits
    /// parsing behavior from PdfStatementFile, with configuration optimized for Barclays-specific layouts.</remarks>
    public class Barclays_PDF_StatementFile: PdfStatementFile
    {
        /// <summary>
        /// Initializes a new instance of the Barclays_PDF_StatementFile class with default parsing settings for
        /// Barclays PDF statements.
        /// </summary>
        /// <param name="logger">The logger instance for logging operations within the statement file processing.</param>
        /// <remarks>This constructor configures the instance to use a minimum table column space size of
        /// 2 and sets the parsing mode to process both text and tables, which is suitable for typical Barclays PDF
        /// statement formats.</remarks>
        public Barclays_PDF_StatementFile(ILogger<Barclays_PDF_StatementFile> logger) : base(logger)
        {
            MinTableColumnSpaceSize = 2;
            ParsingMode = LineParsingMode.TextAndTables;
        }
    }
}

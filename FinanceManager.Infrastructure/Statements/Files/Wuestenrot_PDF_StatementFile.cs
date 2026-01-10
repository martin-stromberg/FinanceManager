using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Statements.Files
{
    /// <summary>
    /// Represents a PDF statement file with parsing logic tailored for Wüstenrot bank statements.
    /// </summary>
    /// <remarks>Use this class to process and extract data from Wüstenrot PDF statement files. Inherits from
    /// PdfStatementFile and configures parsing settings specific to Wüstenrot's document format.</remarks>
    public class Wuestenrot_PDF_StatementFile : PdfStatementFile
    {
        /// <summary>
        /// Initializes a new instance of the Wuestenrot_PDF_StatementFile class with default settings.
        /// </summary>
        /// <param name="logger">The logger instance for logging operations within the statement file processing.</param>
        /// <remarks>This constructor sets the minimum table column space size to 3. Use this constructor
        /// when you want to create a new Wuestenrot_PDF_StatementFile with standard configuration.</remarks>
        public Wuestenrot_PDF_StatementFile(ILogger<Wuestenrot_PDF_StatementFile> logger) : base(logger)
        {
            MinTableColumnSpaceSize = 2;
            ParsingMode = LineParsingMode.TextAndTables;
        }
    }
}

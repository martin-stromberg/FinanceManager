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
        /// Initializes a new instance of the <see cref="Barclays_PDF_StatementFile"/> class with default settings.
        /// </summary>
        public Barclays_PDF_StatementFile() : this(null)
        {
        }
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
        /// <summary>
        /// Loads the specified file and determines whether it contains content associated with BAWAG AG.
        /// </summary>
        /// <remarks>This method checks the first ten lines of the file content to determine if any line
        /// starts with "BAWAG AG". The base class's Load method is called first; if it fails, this method returns
        /// false.</remarks>
        /// <param name="fileName">The name of the file to load. Cannot be null or empty.</param>
        /// <param name="fileBytes">The contents of the file as a byte array. Cannot be null.</param>
        /// <returns>true if the file is loaded successfully and its content matches the expected criteria; otherwise, false.</returns>
        public override bool Load(string fileName, byte[] fileBytes)
        {
            if (!base.Load(fileName, fileBytes))
                return false;

            return ReadContent().Take(10).Any(line => line.StartsWith("BAWAG AG"));
        }
    }
}

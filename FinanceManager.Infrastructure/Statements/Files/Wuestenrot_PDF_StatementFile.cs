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
        /// Initializes a new instance of the <see cref="Wuestenrot_PDF_StatementFile"/> class with default settings.
        /// </summary>
        public Wuestenrot_PDF_StatementFile() : this(null)
        {
        }
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
        /// <summary>
        /// Attempts to load the specified file and verifies whether it contains content associated with Wüstenrot
        /// Bausparkasse AG.
        /// </summary>
        /// <remarks>The method first delegates to the base implementation to perform the initial loading.
        /// It then inspects the file content to determine if it matches the expected format for Wüstenrot Bausparkasse
        /// AG files. Only the first 10 lines are checked for identification.</remarks>
        /// <param name="fileName">The name of the file to load. Cannot be null or empty.</param>
        /// <param name="fileBytes">The contents of the file as a byte array. Cannot be null.</param>
        /// <returns>true if the file is successfully loaded and identified as related to Wüstenrot Bausparkasse AG; otherwise,
        /// false.</returns>
        public override bool Load(string fileName, byte[] fileBytes)
        {
            if (!base.Load(fileName, fileBytes))
                return false;
            return ReadContent().Take(10).Any(line => line.StartsWith("Wüstenrot Bausparkasse AG"));
        }
    }
}

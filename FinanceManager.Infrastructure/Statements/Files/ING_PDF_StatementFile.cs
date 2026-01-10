using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Statements.Files
{
    /// <summary>
    /// Represents a PDF statement file specific to ING, providing parsing and handling functionality tailored to ING's
    /// statement format.
    /// </summary>
    /// <remarks>Use this class to process ING PDF statement files with custom column spacing requirements.
    /// Inherits all standard PDF statement file features from <see cref="PdfStatementFile"/> and applies ING-specific
    /// parsing rules. This class is intended for scenarios where ING's statement layout differs from generic PDF
    /// statements.</remarks>
    public class ING_PDF_StatementFile : PdfStatementFile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ING_PDF_StatementFile"/> class with default settings.
        /// </summary>
        public ING_PDF_StatementFile() : base(null)
        {
        }
        /// <summary>
        /// Loads the specified file and verifies that its content begins with the expected ING-DiBa AG header.
        /// </summary>
        /// <remarks>This method first attempts to load the file using the base implementation. If
        /// successful, it then checks that the file's content begins with the required header. Use this method to
        /// ensure that only files with the correct ING-DiBa AG format are accepted.</remarks>
        /// <param name="fileName">The name of the file to load. Cannot be null or empty.</param>
        /// <param name="fileBytes">The contents of the file as a byte array. Cannot be null.</param>
        /// <returns>true if the file is loaded successfully and its content starts with "ING-DiBa AG"; otherwise, false.</returns>
        public override bool Load(string fileName, byte[] fileBytes)
        {
            var currentParsingMode = ParsingMode;
            ParsingMode = LineParsingMode.TextAndTables;
            if (!base.Load(fileName, fileBytes))
                return false;
            
            var okay = ReadContent().First().StartsWith("ING-DiBa AG");
            if (!okay)
                return false;

            if (ParsingMode != currentParsingMode)
            {
                ParsingMode = currentParsingMode;
                return base.Load(fileName, fileBytes);
            }
            return true;
        }
        /// <summary>
        /// Initializes a new instance of the ING_PDF_StatementFile class with default settings.
        /// </summary>
        /// <param name="logger">The logger instance for logging operations within the statement file processing.</param>
        /// <remarks>This constructor sets the minimum table column space size to 3. Use this constructor
        /// when you want to create a new ING_PDF_StatementFile with standard configuration.</remarks>
        public ING_PDF_StatementFile(ILogger<ING_PDF_StatementFile> logger) : base(logger)
        {
            MinTableColumnSpaceSize = 3;
            ParsingMode = LineParsingMode.TextAndTables;
        }
    }

}

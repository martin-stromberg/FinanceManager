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
        /// Initializes a new instance of the ING_PDF_StatementFile class with default settings.
        /// </summary>
        /// <remarks>This constructor sets the minimum table column space size to 3. Use this constructor
        /// when you want to create a new ING_PDF_StatementFile with standard configuration.</remarks>
        public ING_PDF_StatementFile() : base()
        {
            MinTableColumnSpaceSize = 3;
            ParsingMode = LineParsingMode.TextAndTables;
        }
    }

}

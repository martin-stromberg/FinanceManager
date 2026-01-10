using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Statements.Files
{
    /// <summary>
    /// Represents a bank statement file in the ING CSV format.
    /// </summary>
    /// <remarks>Use this class to parse and process statement files exported from ING in CSV format. Inherits
    /// functionality from the TextStatementFile base class to handle text-based statement files.</remarks>
    public class ING_Csv_StatementFile : TextStatementFile
    {
        /// <summary>
        /// Initializes a new instance of the ING_Csv_StatementFile class with the specified logger.
        /// </summary>
        /// <param name="logger">The logger to use for logging diagnostic and operational information.</param>
        public ING_Csv_StatementFile(ILogger<ING_Csv_StatementFile> logger) : base(logger)
        {
        }
    }

}

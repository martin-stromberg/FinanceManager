using FinanceManager.Application.Statements;
using FinanceManager.Infrastructure.Statements.Files;

namespace FinanceManager.Infrastructure.Statements.Parsers
{
    /// <summary>
    /// Provides a base implementation for statement file parsers that process financial statement files and extract
    /// structured data.
    /// </summary>
    /// <remarks>This abstract class defines the core contract for parsing statement files and extracting both
    /// summary and detailed information. Derived classes should implement the parsing logic specific to the supported
    /// file formats. This class is intended to be used as a base for custom statement file parsers within the
    /// application.</remarks>
    public abstract class BaseStatementFileParser : IStatementFileParser
    {
        /// <summary>
        /// Initializes a new instance of the StatementFileParser class with the specified collection of statement file
        /// readers.
        /// </summary>
        public BaseStatementFileParser()
        {
        }
        /// <summary>
        /// Parses the specified statement file and returns the result of the parsing operation.
        /// </summary>
        /// <param name="statementFile">The statement file to be parsed. Cannot be null.</param>
        /// <returns>A <see cref="StatementParseResult"/> containing the results of the parse operation, or <see
        /// langword="null"/> if the file could not be parsed.</returns>
        public abstract StatementParseResult? Parse(IStatementFile statementFile);
        /// <summary>
        /// Parses the specified statement file and extracts detailed statement information.
        /// </summary>
        /// <param name="statementFile">The statement file to be parsed. Cannot be null.</param>
        /// <returns>A <see cref="StatementParseResult"/> containing the parsed statement details if parsing is successful;
        /// otherwise, <see langword="null"/> if the file could not be parsed.</returns>
        public abstract StatementParseResult? ParseDetails(IStatementFile statementFile);

    }
}

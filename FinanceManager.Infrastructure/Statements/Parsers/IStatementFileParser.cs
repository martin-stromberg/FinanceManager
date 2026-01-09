using FinanceManager.Application.Statements;
using FinanceManager.Infrastructure.Statements.Files;

namespace FinanceManager.Infrastructure.Statements.Parsers
{
    /// <summary>
    /// Interface for parsing statement files and extracting header and movement details.
    /// Implementations should attempt to recognize the file format and extract header information and a list of movements.
    /// </summary>
    public interface IStatementFileParser
    {
        /// <summary>
        /// Parses the specified statement file and returns the result of the parsing operation.
        /// </summary>
        /// <param name="statementFile">The statement file to be parsed. Cannot be null.</param>
        /// <returns>A <see cref="StatementParseResult"/> containing the results of the parsing operation, or <see
        /// langword="null"/> if the file could not be parsed.</returns>
        StatementParseResult? Parse(IStatementFile statementFile);

        /// <summary>
        /// Parses the specified statement file and extracts detailed information about its contents.
        /// </summary>
        /// <param name="statementFile">The statement file to parse. Cannot be null.</param>
        /// <returns>A <see cref="StatementParseResult"/> containing the parsed details if parsing is successful; otherwise, <see
        /// langword="null"/> if the file could not be parsed.</returns>
        StatementParseResult? ParseDetails(IStatementFile statementFile);
    }
}

using FinanceManager.Application.Statements;
using FinanceManager.Infrastructure.Statements.Files;
using Microsoft.Extensions.Logging;

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
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the StatementFileParser class with the specified collection of statement file
        /// readers.
        /// </summary>
        /// <param name="logger">The logger instance used for logging warnings and errors during parsing operations.</param>
        public BaseStatementFileParser(ILogger logger)
        {
            this.logger = logger;
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

        #region Logging Helpers
        /// <summary>
        /// Logs a warning message and associated exception using the configured logger.
        /// </summary>
        /// <param name="ex">The exception to include with the warning log entry. Cannot be null.</param>
        /// <param name="message">The warning message to log. Cannot be null or empty.</param>
        protected void LogWarning(Exception ex, string message)
        {
            logger?.LogWarning(ex, message);
        }
        /// <summary>
        /// Writes a warning message to the configured logger, if available.
        /// </summary>
        /// <param name="message">The warning message to log. Cannot be null.</param>
        protected void LogWarning(string message)
        {
            logger?.LogWarning(message);
        }
        /// <summary>
        /// Logs an error with the specified exception and message to the underlying logging system.
        /// </summary>
        /// <param name="ex">The exception that describes the error to log. Cannot be null.</param>
        /// <param name="message">The message that provides additional context for the error.</param>
        protected void LogError(Exception ex, string message)
        {
            logger?.LogError(ex, message);
        }
        /// <summary>
        /// Writes an informational message to the configured logger, if available.
        /// </summary>
        /// <param name="message">The informational message to log. Cannot be null.</param>
        protected void LogInformation(string message) { 
            logger?.LogInformation(message);
        }
        /// <summary>
        /// Writes a debug-level message to the application's logging system.
        /// </summary>
        /// <param name="message">The message to log at the debug level. This should contain information useful for diagnosing or tracing
        /// application behavior during development.</param>
        protected void LogDebug(string message)
        {
            logger?.LogDebug(message);
        }
        #endregion
    }
}

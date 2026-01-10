using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Statements.Files
{
    /// <summary>
    /// Provides a base implementation for reading the contents of a statement file from a byte array.
    /// </summary>
    /// <remarks>Derived classes should implement the logic for parsing specific statement file formats. This
    /// class defines the contract for reading file content and returning it as a collection of strings.</remarks>
    public abstract class BaseStatementFile : IStatementFile
    {
        /// <summary>
        /// Initializes a new instance of the BaseStatementFile class with the specified logger.
        /// </summary>
        /// <param name="logger">The logger to use for recording diagnostic and operational messages. Cannot be null.</param>
        protected BaseStatementFile(ILogger logger)
        {
            Logger = logger;
        }

        /// <summary>
        /// Gets the underlying byte array representing the data content.
        /// </summary>
        protected byte[] FileBytes { get; private set; }
        /// <summary>
        /// Gets the name of the file associated with this instance.
        /// </summary>
        public string FileName { get; private set; }
        /// <summary>
        /// Gets the logger instance used for recording diagnostic and operational messages.
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Loads the specified file data into the current instance.
        /// </summary>
        /// <param name="fileName">The original filename of the statement file (used for logging or metadata). May be null or empty.</param>
        /// <param name="fileBytes">The byte array containing the file data to load. Cannot be null.</param>
        /// <returns>true if the file data was loaded successfully; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown if fileBytes is null.</exception>
        public virtual bool Load(string fileName, byte[] fileBytes)
        {
            FileBytes = fileBytes ?? throw new ArgumentNullException(nameof(fileBytes));
            FileName = fileName;
            return true;
        }

        /// <summary>
        /// Reads textual content from the specified file data.
        /// </summary>
        /// <returns>An enumerable collection of strings representing the lines or segments of text extracted from the file. The
        /// collection will be empty if no content is found.</returns>
        public abstract IEnumerable<string> ReadContent();
    }
}

namespace FinanceManager.Infrastructure.Statements.Files
{
    /// <summary>
    /// Defines a contract for reading the textual content from a statement file represented as a byte array.
    /// </summary>
    /// <remarks>Implementations of this interface are responsible for interpreting the file format and
    /// extracting lines of text. The interface does not specify the encoding or file type; callers should ensure that
    /// the provided byte array matches the expected format for the implementation.</remarks>
    public interface IStatementFile
    {
        /// <summary>
        /// Gets the name of the file associated with this instance.
        /// </summary>
        string FileName { get; }
        /// <summary>
        /// Loads data from the specified byte array into the current instance.
        /// </summary>
        /// <param name="fileName">The original filename of the statement file (used for logging or metadata). May be null or empty.</param>
        /// <param name="fileBytes">The byte array containing the file data to load. Cannot be null or empty.</param>
        /// <returns>true if the data was loaded successfully; otherwise, false.</returns>
        bool Load(string fileName, byte[] fileBytes);

        /// <summary>
        /// Reads textual content from the specified file data.
        /// </summary>
        /// <returns>An enumerable collection of strings representing the lines of text read from the file. The collection will
        /// be empty if no content is found.</returns>
        IEnumerable<string> ReadContent();
    }
}

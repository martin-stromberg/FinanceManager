namespace FinanceManager.Infrastructure.Statements.Files
{
    /// <summary>
    /// Defines a factory for loading statement files from byte arrays using registered file type handlers.
    /// </summary>
    public interface IStatementFileFactory
    {
        /// <summary>
        /// Loads a statement file from the provided byte array using the registered file types.
        /// </summary>
        /// <param name="fileName">The original filename of the statement file (used for logging or metadata). May be null or empty.</param>
        /// <param name="fileBytes">The byte array containing the file data to load. Cannot be null or empty.</param>
        /// <returns>An instance of <see cref="IStatementFile"/> if a supported file type was recognized and loaded; otherwise, null.</returns>
        IStatementFile? Load(string fileName, byte[] fileBytes);
    }
}

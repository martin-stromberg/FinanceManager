using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Infrastructure.Statements.Files
{
    /// <summary>
    /// Provides functionality to load statement files using a set of supported file types.
    /// </summary>
    /// <remarks>This factory attempts to load a statement file by trying each registered file type in order
    /// until one succeeds. It is typically used to support multiple statement file formats without requiring the caller
    /// to know the specific type in advance.</remarks>
    public class StatementFileFactory: IStatementFileFactory
    {
        private readonly IServiceProvider serviceProvider;
        /// <summary>
        /// Initializes a new instance of the StatementFileFactory class with the specified statement file
        /// implementations.
        /// </summary>
        /// <param name="serviceProvider">The service provider for dependency injection. Cannot be null.</param>
        public StatementFileFactory(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }
        /// <summary>
        /// Attempts to load a statement file from the specified byte array using the available statement file types.
        /// </summary>
        /// <remarks>This method iterates through the supported statement file types and returns the first
        /// one that can successfully load the provided file bytes. If none of the types can load the file, the method
        /// returns <see langword="null"/>.</remarks>
        /// <param name="fileName">The original filename of the statement file (used for logging or metadata). May be null or empty.</param>
        /// <param name="fileBytes">The byte array containing the contents of the statement file to load. Cannot be null.</param>
        /// <returns>An instance of a type that implements <see cref="IStatementFile"/> if the file is successfully loaded;
        /// otherwise, <see langword="null"/>.</returns>
        public IStatementFile? Load(string fileName, byte[] fileBytes)
        {
            foreach (var instance in serviceProvider.GetServices<IStatementFile>())
            {                
                if (instance.Load(fileName, fileBytes))
                    return instance;
            }
            return null;
        }
    }
}

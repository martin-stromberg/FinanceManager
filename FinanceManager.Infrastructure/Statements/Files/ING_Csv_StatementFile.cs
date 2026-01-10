using Microsoft.Extensions.Logging;
using System.Text.Json;

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

        /// <summary>
        /// Loads the specified file and verifies that its content begins with the expected ING-DiBa AG header.
        /// </summary>
        /// <param name="fileName">The path or name of the file to load. Cannot be null or empty.</param>
        /// <param name="fileBytes">The contents of the file as a byte array. Cannot be null.</param>
        /// <returns>true if the file is loaded successfully and its content starts with "ING-DiBa AG"; otherwise, false.</returns>
        public override bool Load(string fileName, byte[] fileBytes)
        {
            if (!base.Load(fileName, fileBytes))
                return false;

            return ReadContent().Take(10).Any(line => line.Contains("Bank;ING"));
        }
    }
    /// <summary>
    /// Represents a statement file that handles backup operations using JSON format.
    /// </summary>
    public class Backup_JSON_StatementFile : TextStatementFile
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        public Backup_JSON_StatementFile(ILogger<Backup_JSON_StatementFile> logger) : base(logger)
        {
        }

        /// <summary>
        /// Loads the specified file and verifies that its content begins with the expected ING-DiBa AG header.
        /// </summary>
        /// <param name="fileName">The path or name of the file to load. Cannot be null or empty.</param>
        /// <param name="fileBytes">The contents of the file as a byte array. Cannot be null.</param>
        /// <returns>true if the file is loaded successfully and its content starts with "ING-DiBa AG"; otherwise, false.</returns>
        public override bool Load(string fileName, byte[] fileBytes)
        {
            if (!base.Load(fileName, fileBytes))
                return false;

            try
            {
                var versionLine = ReadContent().First();
                var doc = JsonDocument.Parse(versionLine);
                var contentType = doc.RootElement.GetProperty("Type").GetString();
                var versionNo = doc.RootElement.GetProperty("Version").GetInt32();
                return contentType == "Backup" && versionNo >= 2;
            }
            catch
            {
                return false;
            }
        }
    }

}

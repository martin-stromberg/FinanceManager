using FinanceManager.Application.Statements;
using System.Text;
using System.Text.Json;

namespace FinanceManager.Infrastructure.Statements.Reader
{
    /// <summary>
    /// Statement file reader that reads a custom backup NDJSON-style file produced by the application.
    /// The reader expects the content to contain metadata followed by a JSON payload and extracts bank account
    /// ledger entries and journal lines to produce a <see cref="StatementParseResult"/>.
    /// </summary>
    public class BackupStatementFileReader : IStatementFileReader
    {
        private BackupData _BackupData = null;
        private StatementHeader _GlobalHeader = null;

        /// <summary>
        /// Represents the deserialized minimal backup payload used by this reader.
        /// Only the fields required for parsing statements are present.
        /// </summary>
        private sealed class BackupData
        {
            /// <summary>Array of bank account objects in the backup.</summary>
            public JsonElement BankAccounts { get; set; }
            /// <summary>Array of ledger entry objects in the backup.</summary>
            public JsonElement BankAccountLedgerEntries { get; set; }
            /// <summary>Array of journal line objects in the backup.</summary>
            public JsonElement BankAccountJournalLines { get; set; }
        }

        /// <summary>
        /// Loads and deserializes the backup payload from the provided bytes.
        /// The method expects the file content to contain a first line (metadata) followed by a JSON payload
        /// which is deserialized into the <see cref="BackupData"/> structure. It also constructs a global
        /// statement header derived from the first bank account found in the payload.
        /// </summary>
        /// <param name="fileBytes">Raw bytes of the uploaded backup file (UTF-8 encoded).</param>
        /// <remarks>
        /// The method may throw <see cref="ArgumentException"/> or JSON parsing exceptions when the input
        /// does not conform to the expected format. Callers should handle exceptions appropriately.
        /// </remarks>
        private void Load(byte[] fileBytes)
        {
            var fileContent = ReadContent(fileBytes);
            var offset = fileContent.IndexOf('\n');
            fileContent = fileContent.Remove(0, offset);
            _BackupData = JsonSerializer.Deserialize<BackupData>(fileContent);
            _GlobalHeader = new StatementHeader()
            {
                IBAN = _BackupData.BankAccounts[0].GetProperty("IBAN").GetString() ?? "",
                Description = $"Backup eingelesen am {DateTime.Today.ToShortDateString()}"
            };
        }

        /// <summary>
        /// Converts the raw file bytes into a normalized string representation used for JSON deserialization.
        /// Normalizes CRLF and CR newlines to LF to ensure predictable slicing by line.
        /// </summary>
        /// <param name="fileBytes">Raw file bytes (expected UTF-8 encoded).</param>
        /// <returns>A string with normalized line endings.</returns>
        private string ReadContent(byte[] fileBytes)
        {
            return Encoding.UTF8.GetString(fileBytes)
                .Replace("\r\n", "\n") // Windows zu Unix
                .Replace("\r", "\n");   // Mac zu Unix;
        }

        /// <summary>
        /// Enumerates statement movements found in the deserialized backup payload.
        /// The method yields <see cref="StatementMovement"/> instances for ledger entries and journal lines
        /// and filters out zero-amount movements.
        /// </summary>
        /// <returns>An enumerable sequence of parsed <see cref="StatementMovement"/> objects.</returns>
        private IEnumerable<StatementMovement> ReadData()
        {
            foreach (var entry in _BackupData.BankAccountLedgerEntries.EnumerateArray())
            {
                var contact = entry.GetProperty("SourceContact");
                var contactUId = (contact.ValueKind == JsonValueKind.Object) ? contact.GetProperty("UID") : new JsonElement();
                var contactId = (contactUId.ValueKind == JsonValueKind.String) ? contactUId.GetGuid() : Guid.Empty;
                var movement = new StatementMovement()
                {
                    BookingDate = entry.GetProperty("PostingDate").GetDateTime(),
                    ValutaDate = entry.GetProperty("ValutaDate").GetDateTime(),
                    Amount = entry.GetProperty("Amount").GetDecimal(),
                    CurrencyCode = entry.GetProperty("CurrencyCode").GetString(),
                    Subject = entry.GetProperty("Description").GetString(),
                    Counterparty = entry.GetProperty("SourceName").GetString(),
                    ContactId = contactId,
                    PostingDescription = entry.GetProperty("PostingDescription").GetString(),
                    IsPreview = false,
                    IsError = false
                };
                if (movement.Amount != 0)
                    yield return movement;
            }

            foreach (var entry in _BackupData.BankAccountJournalLines.EnumerateArray())
            {
                var movement = new StatementMovement()
                {
                    BookingDate = entry.GetProperty("PostingDate").GetDateTime(),
                    ValutaDate = entry.GetProperty("ValutaDate").GetDateTime(),
                    Amount = entry.GetProperty("Amount").GetDecimal(),
                    CurrencyCode = entry.GetProperty("CurrencyCode").GetString(),
                    Subject = entry.GetProperty("Description").GetString(),
                    Counterparty = entry.GetProperty("SourceName").GetString(),
                    PostingDescription = entry.GetProperty("PostingDescription").GetString(),
                    IsPreview = false,
                    IsError = false
                };
                if (movement.Amount != 0)
                    yield return movement;
            }
        }

        /// <summary>
        /// Parses the backup file and returns a <see cref="StatementParseResult"/> containing the header and list of movements.
        /// If parsing fails the method returns <c>null</c>.
        /// </summary>
        /// <param name="fileName">Original file name (not used by this reader but part of the contract).</param>
        /// <param name="fileBytes">Raw bytes of the uploaded backup file.</param>
        /// <returns>A <see cref="StatementParseResult"/> on success, or <c>null</c> when parsing fails.</returns>
        public StatementParseResult? Parse(string fileName, byte[] fileBytes)
        {
            try
            {
                Load(fileBytes);
                return new StatementParseResult(_GlobalHeader, ReadData().ToList());
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Parses supplemental details from the backup file. For this reader details and the regular parse share the same implementation.
        /// Returns <c>null</c> when parsing fails.
        /// </summary>
        /// <param name="originalFileName">Original file name of the uploaded document.</param>
        /// <param name="fileBytes">Raw file bytes to parse.</param>
        /// <returns>A <see cref="StatementParseResult"/> on success, otherwise <c>null</c>.</returns>
        public StatementParseResult? ParseDetails(string originalFileName, byte[] fileBytes)
        {
            try
            {
                Load(fileBytes);
                return new StatementParseResult(_GlobalHeader, ReadData().ToList());
            }
            catch
            {
                return null;
            }
        }
    }
}

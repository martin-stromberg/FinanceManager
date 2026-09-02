using FinanceManager.Application.Statements;
using FinanceManager.Infrastructure.Statements.Files;
using System.Text;
using System.Text.Json;

namespace FinanceManager.Infrastructure.Statements.Parsers
{
    /// <summary>
    /// Statement file reader that reads a custom backup NDJSON-style file produced by the application.
    /// The reader expects the content to contain metadata followed by a JSON payload and extracts bank account
    /// ledger entries and journal lines to produce a <see cref="StatementParseResult"/>.
    /// </summary>
    public class Backup_JSON_StatementFileParser : IStatementFileParser
    {
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
        /// Holds the outcome of <see cref="Load"/>: the statement header derived from the backup and the raw
        /// deserialized backup payload used to enumerate movements.
        /// </summary>
        private sealed class LoadResult
        {
            /// <summary>Gets the statement header derived from the backup payload.</summary>
            public required StatementHeader Header { get; init; }
            /// <summary>Gets the deserialized backup payload.</summary>
            public required BackupData Data { get; init; }
        }

        /// <summary>
        /// Reads and deserializes the backup payload from the given statement file.
        /// </summary>
        /// <param name="statementFile"></param>
        /// <returns>The parsed statement header together with the deserialized backup data payload.</returns>
        /// <exception cref="FormatException">Thrown when the file content is not a valid backup JSON payload
        /// (e.g. it deserializes to <see langword="null"/>).</exception>
        private LoadResult Load(IStatementFile statementFile)
        {
            var fileContent = string.Join("\r\n", statementFile.ReadContent()).Replace("\r\n", "\n").Replace("\r", "\n");
            var offset = fileContent.IndexOf('\n');
            fileContent = fileContent.Remove(0, offset);
            var data = JsonSerializer.Deserialize<BackupData>(fileContent)
                ?? throw new FormatException("Backup JSON payload could not be deserialized into the expected structure.");
            var header = new StatementHeader()
            {
                IBAN = data.BankAccounts[0].GetProperty("IBAN").GetString() ?? "",
                Description = $"Backup eingelesen am {DateTime.Today.ToShortDateString()}"
            };
            return new LoadResult { Header = header, Data = data };
        }

        /// <summary>
        /// Enumerates statement movements found in the deserialized backup payload.
        /// The method yields <see cref="StatementMovement"/> instances for ledger entries and journal lines
        /// and filters out zero-amount movements.
        /// </summary>
        /// <param name="data">The deserialized backup payload to read movements from.</param>
        /// <returns>An enumerable sequence of parsed <see cref="StatementMovement"/> objects.</returns>
        private IEnumerable<StatementMovement> ReadData(BackupData data)
        {
            foreach (var entry in data.BankAccountLedgerEntries.EnumerateArray())
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

            foreach (var entry in data.BankAccountJournalLines.EnumerateArray())
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
        /// Parses the specified statement file and returns the result if parsing is successful.
        /// </summary>
        /// <param name="statementFile">The statement file to parse. Cannot be null.</param>
        /// <returns>A list containing a single <see cref="StatementParseResult"/> if parsing succeeds; otherwise, <see
        /// langword="null"/>.</returns>
        public IReadOnlyList<StatementParseResult>? Parse(IStatementFile statementFile)
        {
            try
            {
                var loaded = Load(statementFile);
                return new List<StatementParseResult> { new StatementParseResult(loaded.Header, ReadData(loaded.Data).ToList()) };
            }
            catch
            {
                return null;
            }
        }
        /// <summary>
        /// Parses the specified statement file and returns the extracted details if parsing is successful.
        /// </summary>
        /// <remarks>If an error occurs during parsing, the method returns <see langword="null"/> instead
        /// of throwing an exception.</remarks>
        /// <param name="statementFile">The statement file to parse. Cannot be null.</param>
        /// <returns>A list containing a single <see cref="StatementParseResult"/> if parsing succeeds; otherwise,
        /// <see langword="null"/>.</returns>
        public IReadOnlyList<StatementParseResult>? ParseDetails(IStatementFile statementFile)
        {
            return Parse(statementFile);
        }
    }
}

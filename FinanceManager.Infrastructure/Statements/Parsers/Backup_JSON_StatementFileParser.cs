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
        /// 
        /// </summary>
        /// <param name="statementFile"></param>
        private void Load(IStatementFile statementFile)
        {
            var fileContent = string.Join("\r\n", statementFile.ReadContent()).Replace("\r\n", "\n").Replace("\r", "\n");
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
        /// Parses the specified statement file and returns the result if parsing is successful.
        /// </summary>
        /// <param name="statementFile">The statement file to parse. Cannot be null.</param>
        /// <returns>A <see cref="StatementParseResult"/> containing the parsed data if parsing succeeds; otherwise, <see
        /// langword="null"/>.</returns>
        public StatementParseResult? Parse(IStatementFile statementFile)
        {
            try
            {
                Load(statementFile);
                return new StatementParseResult(_GlobalHeader, ReadData().ToList());
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
        /// <returns>A <see cref="StatementParseResult"/> containing the parsed header and data if parsing succeeds; otherwise,
        /// <see langword="null"/>.</returns>
        public StatementParseResult? ParseDetails(IStatementFile statementFile)
        {
            try
            {
                Load(statementFile);
                return new StatementParseResult(_GlobalHeader, ReadData().ToList());
            }
            catch
            {
                return null;
            }
        }
    }
}

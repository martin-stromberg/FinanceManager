namespace FinanceManager.Application.Reports;

/// <summary>
/// Query object used to request an export of statement draft entries.
/// </summary>
/// <param name="OwnerUserId">Owner user identifier performing the export.</param>
/// <param name="DraftId">Statement draft id whose entries are exported.</param>
/// <param name="Format">Export format.</param>
/// <param name="MaxRows">Maximum number of rows to include.</param>
/// <param name="From">Optional from date filter.</param>
/// <param name="To">Optional to date filter.</param>
/// <param name="Q">Optional search query.</param>
/// <returns>The result.</returns>
public sealed record StatementDraftExportQuery(
    Guid OwnerUserId,
    Guid DraftId,
    PostingExportFormat Format,
    int MaxRows,
    DateTime? From = null,
    DateTime? To = null,
    string? Q = null
);

/// <summary>
/// Single row representation used when streaming or generating statement draft entry export content.
/// </summary>
/// <param name="EntryNumber">Sequential entry number within the draft.</param>
/// <param name="BookingDate">Booking date of the entry.</param>
/// <param name="ValutaDate">Optional valuta/value date of the entry.</param>
/// <param name="Amount">Monetary amount.</param>
/// <param name="CurrencyCode">Currency code of the amount.</param>
/// <param name="Subject">Optional subject/short description.</param>
/// <param name="RecipientName">Optional recipient name.</param>
/// <param name="BookingDescription">Optional booking description text.</param>
/// <param name="Status">Processing status of the entry.</param>
/// <param name="IsAnnounced">Whether the entry was announced (pre-booked) in the source.</param>
/// <param name="IsCostNeutral">Whether the entry is cost-neutral.</param>
/// <param name="ContactId">Optional contact id referenced.</param>
/// <param name="SavingsPlanId">Optional savings plan id referenced.</param>
/// <param name="SecurityId">Optional security id referenced.</param>
/// <param name="SecurityTransactionType">Optional security transaction type.</param>
/// <param name="SecurityQuantity">Optional quantity for security transactions.</param>
/// <param name="SecurityFeeAmount">Optional fee amount for the security transaction.</param>
/// <param name="SecurityTaxAmount">Optional tax amount for the security transaction.</param>
/// <returns>The result.</returns>
public sealed record StatementDraftEntryExportRow(
    int EntryNumber,
    DateTime BookingDate,
    DateTime? ValutaDate,
    decimal Amount,
    string CurrencyCode,
    string? Subject,
    string? RecipientName,
    string? BookingDescription,
    StatementDraftEntryStatus Status,
    bool IsAnnounced,
    bool IsCostNeutral,
    Guid? ContactId,
    Guid? SavingsPlanId,
    Guid? SecurityId,
    SecurityTransactionType? SecurityTransactionType,
    decimal? SecurityQuantity,
    decimal? SecurityFeeAmount,
    decimal? SecurityTaxAmount
);

/// <summary>
/// Service supporting querying and generation of statement draft entry exports in various formats.
/// </summary>
public interface IStatementDraftExportService
{
    /// <summary>
    /// Streams statement draft entry export rows matching the given query.
    /// </summary>
    /// <param name="query">Query describing the export criteria and limits.</param>
    /// <param name="ct">Cancellation token to cancel the streaming operation.</param>
    /// <returns>An async sequence of <see cref="StatementDraftEntryExportRow"/> instances matching the query.</returns>
    IAsyncEnumerable<StatementDraftEntryExportRow> QueryAsync(StatementDraftExportQuery query, CancellationToken ct);

    /// <summary>
    /// Generates a file for the given query and returns content type, file name and a readable stream with the generated content.
    /// Caller is responsible for disposing the returned <see cref="Stream"/> when finished.
    /// </summary>
    /// <param name="query">Query describing the export to generate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A tuple containing the content type (e.g. "text/csv"), the suggested file name and a stream with file content.
    /// The returned stream is readable from the beginning.
    /// </returns>
    Task<(string ContentType, string FileName, Stream Content)> GenerateAsync(StatementDraftExportQuery query, CancellationToken ct);

    /// <summary>
    /// Counts the total number of rows that would be produced for the given export query.
    /// </summary>
    /// <param name="query">Export query to evaluate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Total number of matching rows (non-negative integer).</returns>
    Task<int> CountAsync(StatementDraftExportQuery query, CancellationToken ct);

    /// <summary>
    /// Streams CSV content for the given query directly to the provided output stream.
    /// This method writes CSV text into the supplied <paramref name="output"/> stream and does not return a separate buffer.
    /// </summary>
    /// <param name="query">Export query describing which rows to include.</param>
    /// <param name="output">Destination stream to which CSV content will be written. The caller remains owner of the stream.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when streaming has finished or an exception is thrown.</returns>
    Task StreamCsvAsync(StatementDraftExportQuery query, Stream output, CancellationToken ct);
}

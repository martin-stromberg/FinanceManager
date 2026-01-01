namespace FinanceManager.Application.Statements;

using System;
using System.Collections.Generic;

/// <summary>
/// Interface for parsing statement files and extracting header and movement details.
/// Implementations should attempt to recognize the file format and extract header information and a list of movements.
/// </summary>
public interface IStatementFileReader
{
    /// <summary>
    /// Attempts to parse statement contents and produce a <see cref="StatementParseResult"/>.
    /// This method is the primary parsing entry and may include format detection and mapping of movements.
    /// Implementations return <c>null</c> when the provided bytes do not represent a supported format.
    /// </summary>
    /// <param name="fileName">The original file name (used for format hints and metadata extraction).</param>
    /// <param name="fileBytes">The raw file content as a byte array.</param>
    /// <returns>
    /// A <see cref="StatementParseResult"/> containing parsed header and movements when parsing succeeded;
    /// otherwise <c>null</c> when the format is not supported or parsing failed.
    /// </returns>
    StatementParseResult? Parse(string fileName, byte[] fileBytes);

    /// <summary>
    /// Attempts to parse only supplemental details from statement files (for example fees, taxes or security quantities)
    /// that may be provided in separate files or detail exports. Returns <c>null</c> when parsing fails or no details found.
    /// </summary>
    /// <param name="originalFileName">The original file name (used for format hints and metadata extraction).</param>
    /// <param name="fileBytes">The raw file content as a byte array.</param>
    /// <returns>
    /// A <see cref="StatementParseResult"/> with header and movements when details were successfully extracted;
    /// otherwise <c>null</c> when no details could be parsed or the format is unsupported.
    /// </returns>
    StatementParseResult? ParseDetails(string originalFileName, byte[] fileBytes);
}

/// <summary>
/// Statement file header information extracted by a reader.
/// </summary>
public sealed class StatementHeader
{
    /// <summary>
    /// Account number found in the statement.
    /// </summary>
    /// <value>Account number string; empty when not present.</value>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// Optional IBAN found in the statement.
    /// </summary>
    /// <value>IBAN string or <c>null</c> when not present.</value>
    public string? IBAN { get; set; }

    /// <summary>
    /// Optional bank code or BLZ.
    /// </summary>
    /// <value>Bank code string or <c>null</c> when not present.</value>
    public string? BankCode { get; set; }

    /// <summary>
    /// Optional account holder name.
    /// </summary>
    /// <value>Account holder name or <c>null</c> when not present.</value>
    public string? AccountHolder { get; set; }

    /// <summary>
    /// Optional period start date from the statement.
    /// </summary>
    /// <value>Start date of the statement period or <c>null</c>.</value>
    public DateTime? PeriodStart { get; set; }

    /// <summary>
    /// Optional period end date from the statement.
    /// </summary>
    /// <value>End date of the statement period or <c>null</c>.</value>
    public DateTime? PeriodEnd { get; set; }

    /// <summary>
    /// Human readable description found in the statement header.
    /// </summary>
    /// <value>Description text; empty when not present.</value>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Represents a single movement/transaction parsed from a statement file.
/// </summary>
public sealed class StatementMovement
{
    /// <summary>
    /// Booking date of the movement.
    /// </summary>
    /// <value>Booking date.</value>
    public DateTime BookingDate { get; set; }

    /// <summary>
    /// Amount associated with the movement.
    /// </summary>
    /// <value>Monetary amount (positive/negative).</value>
    public decimal Amount { get; set; }

    /// <summary>
    /// Optional subject or brief description.
    /// </summary>
    /// <value>Subject text or <c>null</c>.</value>
    public string? Subject { get; set; }

    /// <summary>
    /// Optional counterparty name.
    /// </summary>
    /// <value>Counterparty name or <c>null</c>.</value>
    public string? Counterparty { get; set; }

    /// <summary>
    /// Valuta / value date of the movement.
    /// </summary>
    /// <value>Valuta/date-of-value.</value>
    public DateTime ValutaDate { get; set; }

    /// <summary>
    /// Optional posting description.
    /// </summary>
    /// <value>Longer booking description or <c>null</c>.</value>
    public string? PostingDescription { get; set; }

    /// <summary>
    /// Optional currency code.
    /// </summary>
    /// <value>ISO currency code (e.g. "EUR") or <c>null</c>.</value>
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// Indicates if the movement is a preview (not final in the source system).
    /// </summary>
    /// <value><c>true</c> when the movement is a preview; otherwise <c>false</c>.</value>
    public bool IsPreview { get; set; }

    /// <summary>
    /// Indicates if there was an error while parsing or interpreting the movement.
    /// </summary>
    /// <value><c>true</c> when the movement contains an error marker; otherwise <c>false</c>.</value>
    public bool IsError { get; set; }

    /// <summary>
    /// Associated contact ID as identified by the parser (may be <c>Guid.Empty</c> when not resolved).
    /// </summary>
    /// <value>Contact identifier.</value>
    public Guid ContactId { get; set; }

    /// <summary>
    /// Optional quantity related to the movement (e.g., securities quantity).
    /// </summary>
    /// <value>Quantity or <c>null</c>.</value>
    public decimal? Quantity { get; set; }

    /// <summary>
    /// Optional tax amount related to the movement.
    /// </summary>
    /// <value>Tax amount or <c>null</c>.</value>
    public decimal? TaxAmount { get; set; }

    /// <summary>
    /// Optional fee amount related to the movement.
    /// </summary>
    /// <value>Fee amount or <c>null</c>.</value>
    public decimal? FeeAmount { get; set; }
};

/// <summary>
/// Result of parsing a statement file. Contains an extracted header and the list of movements.
/// </summary>
public sealed class StatementParseResult
{
    /// <summary>
    /// Creates a new instance of <see cref="StatementParseResult"/>.
    /// </summary>
    /// <param name="header">Parsed statement header. Must not be null.</param>
    /// <param name="movements">Parsed movements. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="header"/> or <paramref name="movements"/> is null.</exception>
    public StatementParseResult(StatementHeader header, IReadOnlyCollection<StatementMovement> movements)
    {
        Header = header ?? throw new ArgumentNullException(nameof(header));
        Movements = movements ?? throw new ArgumentNullException(nameof(movements));
    }

    /// <summary>
    /// Parsed statement header information.
    /// </summary>
    public StatementHeader Header { get; }

    /// <summary>
    /// Collection of parsed movements/transactions from the statement file.
    /// </summary>
    public IReadOnlyCollection<StatementMovement> Movements { get; }
}
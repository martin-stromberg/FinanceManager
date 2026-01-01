namespace FinanceManager.Domain.Statements;

/// <summary>
/// Represents a single entry parsed from an imported statement. Contains booking information and optional
/// resolved references for accounting (contact, savings plan, security transaction).
/// </summary>
public sealed class StatementEntry : Entity
{
    /// <summary>
    /// Parameterless constructor for ORM/deserialization.
    /// </summary>
    private StatementEntry() { }

    /// <summary>
    /// Creates a new <see cref="StatementEntry"/> for a given statement import.
    /// </summary>
    /// <param name="statementImportId">Identifier of the owning statement import. Must not be <see cref="Guid.Empty"/>.</param>
    /// <param name="bookingDate">Booking date of the entry.</param>
    /// <param name="amount">Monetary amount. Zero is allowed for neutral items.</param>
    /// <param name="subject">Subject or short description. Must not be null or whitespace.</param>
    /// <param name="rawHash">A hash identifying the raw line content for deduplication. Must not be null or whitespace.</param>
    /// <param name="recipientName">Optional recipient name extracted from the source.</param>
    /// <param name="valutaDate">Optional valuta/date-of-value for the amount.</param>
    /// <param name="currencyCode">Optional currency code (defaults to "EUR" when null or whitespace).</param>
    /// <param name="bookingDescription">Optional longer booking description.</param>
    /// <param name="isAnnounced">Whether the entry was announced/pre-booked in the source system.</param>
    /// <param name="isCostNeutral">Whether the entry is cost-neutral and should not affect totals.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="statementImportId"/> is <see cref="Guid.Empty"/>, or when <paramref name="subject"/> or <paramref name="rawHash"/> are null/whitespace (via guards).</exception>
    public StatementEntry(Guid statementImportId, DateTime bookingDate, decimal amount, string subject, string rawHash, string? recipientName, DateTime? valutaDate, string? currencyCode, string? bookingDescription, bool isAnnounced, bool isCostNeutral)
    {
        StatementImportId = Guards.NotEmpty(statementImportId, nameof(statementImportId));
        BookingDate = bookingDate;
        Amount = amount; // 0 amount allowed for neutral items (fees, etc.)
        Subject = Guards.NotNullOrWhiteSpace(subject, nameof(subject));
        RawHash = Guards.NotNullOrWhiteSpace(rawHash, nameof(rawHash));
        RecipientName = recipientName;
        ValutaDate = valutaDate;
        CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "EUR" : currencyCode!;
        BookingDescription = bookingDescription;
        IsAnnounced = isAnnounced;
        IsCostNeutral = isCostNeutral;
        Status = StatementEntryStatus.Pending;
    }

    /// <summary>
    /// Identifier of the owning statement import.
    /// </summary>
    /// <value>The import GUID.</value>
    public Guid StatementImportId { get; private set; }

    /// <summary>
    /// Booking date of the entry.
    /// </summary>
    public DateTime BookingDate { get; private set; }

    /// <summary>
    /// Optional valuta/date-of-value for the entry.
    /// </summary>
    public DateTime? ValutaDate { get; private set; }

    /// <summary>
    /// Monetary amount of the entry.
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>
    /// Short subject or description.
    /// </summary>
    public string Subject { get; private set; } = null!;

    /// <summary>
    /// Raw content hash used for deduplication of imported lines.
    /// </summary>
    public string RawHash { get; private set; } = null!;

    /// <summary>
    /// Optional recipient name extracted from the statement.
    /// </summary>
    public string? RecipientName { get; private set; }

    /// <summary>
    /// Currency code for the amount (ISO code). Default is "EUR".
    /// </summary>
    public string CurrencyCode { get; private set; } = "EUR";

    /// <summary>
    /// Optional longer booking description.
    /// </summary>
    public string? BookingDescription { get; private set; }

    /// <summary>
    /// Whether the entry was announced/pre-booked in the source system.
    /// </summary>
    public bool IsAnnounced { get; private set; }

    /// <summary>
    /// Whether the entry is cost-neutral and should not affect totals.
    /// </summary>
    public bool IsCostNeutral { get; private set; }

    /// <summary>
    /// Optional resolved contact id when the entry is matched to a contact.
    /// </summary>
    public Guid? ContactId { get; private set; }

    /// <summary>
    /// Optional assigned savings plan id when the entry is matched to a savings plan.
    /// </summary>
    public Guid? SavingsPlanId { get; private set; }

    /// <summary>
    /// Optional security transaction id when the entry is matched to a security booking.
    /// </summary>
    public Guid? SecurityTransactionId { get; private set; }

    /// <summary>
    /// Processing status of this imported entry.
    /// </summary>
    public StatementEntryStatus Status { get; private set; } = StatementEntryStatus.Pending;
}
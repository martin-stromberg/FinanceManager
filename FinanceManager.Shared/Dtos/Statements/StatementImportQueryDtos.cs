using FinanceManager.Shared.Dtos.Accounts;

namespace FinanceManager.Shared.Dtos.Statements;

/// <summary>
/// Read projection of a committed statement import (<c>StatementImport</c>) for the
/// <c>IStatementImportQueryService</c> read contract (toolkatalog T08). Statement imports
/// are revision-safe documents: this DTO is emitted by read paths only and carries no write
/// payload. <see cref="AccountType"/> is denormalized so consumers can evaluate account-type
/// restrictions without a second lookup.
/// </summary>
/// <param name="Id">Import identifier.</param>
/// <param name="AccountId">Anchor account of the import (ownership is derived via this account).</param>
/// <param name="AccountType">Type of the anchor account.</param>
/// <param name="Format">Original import format.</param>
/// <param name="ImportedAtUtc">Commit timestamp (UTC).</param>
/// <param name="OriginalFileName">Original file name of the imported document (untrusted content).</param>
/// <param name="TotalEntries">Number of statement entries in this import.</param>
public sealed record StatementImportListItemDto(
    Guid Id,
    Guid AccountId,
    AccountType AccountType,
    ImportFormat Format,
    DateTime ImportedAtUtc,
    string OriginalFileName,
    int TotalEntries);

/// <summary>
/// Read projection of one committed statement entry (<c>StatementEntry</c>, status
/// Pending/Booked/IgnoredDuplicate). Free-text fields (<see cref="Subject"/>,
/// <see cref="RecipientName"/>, <see cref="BookingDescription"/>) originate from imported
/// documents and are untrusted content (sicherheitskonzept.md §5).
/// </summary>
/// <param name="Id">Entry identifier.</param>
/// <param name="StatementImportId">Parent import identifier.</param>
/// <param name="BookingDate">Booking date.</param>
/// <param name="ValutaDate">Value date.</param>
/// <param name="Amount">Booked amount.</param>
/// <param name="CurrencyCode">ISO currency code.</param>
/// <param name="Subject">Imported booking subject (untrusted content).</param>
/// <param name="RecipientName">Imported recipient/counterparty (untrusted content).</param>
/// <param name="BookingDescription">Imported booking text (untrusted content).</param>
/// <param name="IsAnnounced">Whether the entry was announced.</param>
/// <param name="IsCostNeutral">Whether the entry is cost neutral.</param>
/// <param name="ContactId">Assigned contact id.</param>
/// <param name="SavingsPlanId">Assigned savings plan id.</param>
/// <param name="SecurityTransactionId">Linked security transaction id.</param>
/// <param name="Status">Committed entry status (Pending/Booked/IgnoredDuplicate).</param>
public sealed record StatementImportEntryDto(
    Guid Id,
    Guid StatementImportId,
    DateTime BookingDate,
    DateTime? ValutaDate,
    decimal Amount,
    string CurrencyCode,
    string Subject,
    string? RecipientName,
    string? BookingDescription,
    bool IsAnnounced,
    bool IsCostNeutral,
    Guid? ContactId,
    Guid? SavingsPlanId,
    Guid? SecurityTransactionId,
    StatementEntryStatus Status);

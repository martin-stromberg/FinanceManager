using FinanceManager.Shared.Dtos.Securities;

namespace FinanceManager.Shared.Dtos.Reports;

/// <summary>
/// A single posting row shown in the report postings drill-down overlay.
/// </summary>
/// <param name="Id">Posting id.</param>
/// <param name="BookingDate">Booking date.</param>
/// <param name="ValutaDate">Valuta date (<see cref="DateTime.MinValue"/> when absent).</param>
/// <param name="Amount">Posting amount.</param>
/// <param name="Kind">Posting kind.</param>
/// <param name="Subject">Optional subject.</param>
/// <param name="RecipientName">Optional recipient name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="AccountId">Referenced account id, if any.</param>
/// <param name="ContactId">Referenced contact id, if any.</param>
/// <param name="SavingsPlanId">Referenced savings plan id, if any.</param>
/// <param name="SecurityId">Referenced security id, if any.</param>
/// <param name="SecuritySubType">Optional security posting sub type.</param>
/// <param name="EntityName">Display name of the entity the posting references (its origin).</param>
/// <returns>The result.</returns>
public sealed record ReportPostingDto(
    Guid Id,
    DateTime BookingDate,
    DateTime ValutaDate,
    decimal Amount,
    PostingKind Kind,
    string? Subject,
    string? RecipientName,
    string? Description,
    Guid? AccountId,
    Guid? ContactId,
    Guid? SavingsPlanId,
    Guid? SecurityId,
    SecurityPostingSubType? SecuritySubType,
    string EntityName
);

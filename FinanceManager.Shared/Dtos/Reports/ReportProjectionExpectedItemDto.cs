namespace FinanceManager.Shared.Dtos.Reports;

/// <summary>
/// Details for a prior-year posting event that is still expected in the projected report period.
/// </summary>
/// <param name="Kind">The posting kind of the entity the expectation belongs to.</param>
/// <param name="EntityId">The id of the entity (account, contact, savings plan or security).</param>
/// <param name="EntityName">The display name of the entity.</param>
/// <param name="ExpectedDate">The expected date.</param>
/// <param name="PriorYearDate">The prior year date.</param>
/// <param name="Amount">The expected amount.</param>
/// <param name="PostingId">The prior-year posting that produced the expectation, when identifiable.</param>
/// <returns>The result.</returns>
public sealed record ReportProjectionExpectedItemDto(
    PostingKind Kind,
    Guid EntityId,
    string EntityName,
    DateTime ExpectedDate,
    DateTime PriorYearDate,
    decimal Amount,
    Guid? PostingId = null);

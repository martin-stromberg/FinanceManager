namespace FinanceManager.Shared.Dtos.Reports;

/// <summary>
/// A suppressed projection expectation scoped to one report favorite.
/// </summary>
/// <param name="Id">The suppression identifier.</param>
/// <param name="Kind">The posting kind of the entity the expectation belongs to.</param>
/// <param name="EntityId">The id of the entity (account, contact, savings plan or security).</param>
/// <param name="EntityName">The display name of the entity.</param>
/// <param name="ExpectedDate">The date the posting would have been expected (prior year date + 1 year).</param>
/// <param name="PriorYearDate">The date of the prior-year posting whose projection is suppressed.</param>
/// <param name="SuppressedAtUtc">When the expectation was suppressed (UTC).</param>
/// <param name="Amount">The expected amount, when known.</param>
/// <param name="PostingId">The prior-year posting that produced the expectation, when identifiable.</param>
/// <returns>The result.</returns>
public sealed record ReportSuppressedExpectationDto(
    Guid Id,
    PostingKind Kind,
    Guid EntityId,
    string EntityName,
    DateTime ExpectedDate,
    DateTime PriorYearDate,
    DateTime SuppressedAtUtc,
    decimal? Amount = null,
    Guid? PostingId = null);

using System.Text.Json.Serialization;

namespace FinanceManager.Shared.Dtos.Reports;

/// <summary>
/// Request payload to suppress one expected posting in the context of a report favorite.
/// The amount is intentionally not part of the suppression identity so amount corrections on
/// the prior-year posting do not lift the suppression.
/// </summary>
/// <param name="Kind">The posting kind of the expected item.</param>
/// <param name="EntityId">The id of the entity the expectation belongs to.</param>
/// <param name="PriorYearDate">The date of the prior-year posting whose projection is suppressed.</param>
/// <param name="PostingId">The prior-year posting that produced the expectation, when identifiable.</param>
/// <param name="Amount">Expected amount displayed in the suppressed-expectations overlay.</param>
/// <returns>The result.</returns>
[method: JsonConstructor]
public sealed record ReportSuppressedExpectationRequest(
    PostingKind Kind,
    Guid EntityId,
    DateTime PriorYearDate,
    Guid? PostingId = null,
    decimal? Amount = null);

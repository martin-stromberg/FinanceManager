using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Shared.Dtos.Accounts;

/// <summary>
/// Request payload for creating or updating a posting rule of a bank account.
/// </summary>
/// <param name="Id">Identifier of an existing rule to update; <c>null</c> to create a new rule.</param>
/// <param name="Field">The text field the rule matches against.</param>
/// <param name="Pattern">The text that must be contained in the selected field.</param>
/// <param name="PostingType">The posting type assigned when the rule matches.</param>
/// <param name="OrderIndex">Evaluation order; the lowest matching rule wins. When creating a rule (<paramref name="Id"/> is <c>null</c>), <c>null</c> appends the rule at the end. On update <c>null</c> keeps the current position.</param>
/// <returns>The result.</returns>
public sealed record AccountPostingRuleUpsertRequest(
    Guid? Id,
    PostingRuleField Field,
    [Required, MaxLength(200)] string Pattern,
    BankPostingType PostingType,
    int? OrderIndex);

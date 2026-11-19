namespace FinanceManager.Shared.Dtos.Accounts;

/// <summary>
/// DTO representing a posting rule (Kontierungsregel) of a bank account.
/// </summary>
/// <param name="Id">Identifier of the posting rule.</param>
/// <param name="Field">The text field the rule matches against.</param>
/// <param name="Pattern">The text that must be contained in the selected field.</param>
/// <param name="PostingType">The posting type assigned when the rule matches.</param>
/// <param name="OrderIndex">Evaluation order; the lowest matching rule wins.</param>
/// <returns>The result.</returns>
public sealed record AccountPostingRuleDto(
    Guid Id,
    PostingRuleField Field,
    string Pattern,
    BankPostingType PostingType,
    int OrderIndex);

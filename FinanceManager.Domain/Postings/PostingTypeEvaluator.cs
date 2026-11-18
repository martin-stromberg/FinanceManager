using System.Text.RegularExpressions;
using FinanceManager.Domain.Accounts;

namespace FinanceManager.Domain.Postings;

/// <summary>
/// Central evaluator that determines the posting type (<see cref="BankPostingType"/>) of a bank posting or
/// statement draft entry: first by applying the account's posting rules (text field contains text),
/// then by falling back to the amount sign.
/// </summary>
public static class PostingTypeEvaluator
{
    /// <summary>
    /// Normalizes German umlauts in the provided text to ASCII-friendly replacements.
    /// Example: "ä" -> "ae", "ß" -> "ss".
    /// </summary>
    /// <param name="text">Input text to normalize. May be <c>null</c> or empty.</param>
    /// <returns>A new string with umlauts replaced, or an empty string when <paramref name="text"/> is null or empty.</returns>
    public static string NormalizeUmlauts(string? text)
    {
        if (string.IsNullOrEmpty(text)) { return string.Empty; }
        return text
            .Replace("ä", "ae", StringComparison.OrdinalIgnoreCase)
            .Replace("ö", "oe", StringComparison.OrdinalIgnoreCase)
            .Replace("ü", "ue", StringComparison.OrdinalIgnoreCase)
            .Replace("ß", "ss", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes a text for rule matching: umlaut replacement, lower casing and whitespace removal.
    /// </summary>
    /// <param name="text">Input text to normalize. May be <c>null</c>.</param>
    /// <returns>The normalized text used for substring matching.</returns>
    public static string NormalizeForRuleMatch(string? text)
        => Regex.Replace(NormalizeUmlauts(text ?? string.Empty).ToLowerInvariant(), "\\s+", string.Empty);

    /// <summary>
    /// Evaluates the posting type for a bank posting or draft entry.
    /// Rules are evaluated in <see cref="AccountPostingRule.OrderIndex"/> order (tiebreak by <see cref="Entity.Id"/>);
    /// the first matching rule wins. Without a match the amount sign decides
    /// (negative amount -> <see cref="BankPostingType.Outflow"/>, otherwise <see cref="BankPostingType.Inflow"/>).
    /// </summary>
    /// <param name="rules">The posting rules of the account. May be empty.</param>
    /// <param name="subject">The subject (purpose) text. May be <c>null</c>.</param>
    /// <param name="bookingDescription">The booking description text. May be <c>null</c>.</param>
    /// <param name="amount">The posting amount.</param>
    /// <returns>The determined <see cref="BankPostingType"/> (never <see cref="BankPostingType.Unspecified"/>).</returns>
    public static BankPostingType Evaluate(IReadOnlyCollection<AccountPostingRule> rules, string? subject, string? bookingDescription, decimal amount)
    {
        if (rules is { Count: > 0 })
        {
            var normalizedSubject = NormalizeForRuleMatch(subject);
            var normalizedDescription = NormalizeForRuleMatch(bookingDescription);
            foreach (var rule in rules.OrderBy(r => r.OrderIndex).ThenBy(r => r.Id))
            {
                var normalizedPattern = NormalizeForRuleMatch(rule.Pattern);
                if (string.IsNullOrEmpty(normalizedPattern)) { continue; }
                var haystack = rule.Field == PostingRuleField.BookingDescription ? normalizedDescription : normalizedSubject;
                if (haystack.Contains(normalizedPattern, StringComparison.Ordinal))
                {
                    return rule.PostingType;
                }
            }
        }
        return EvaluateByAmountSign(amount);
    }

    /// <summary>
    /// Classifies a posting type purely by the amount sign: a negative amount yields
    /// <see cref="BankPostingType.Outflow"/>, a zero or positive amount <see cref="BankPostingType.Inflow"/>.
    /// </summary>
    /// <param name="amount">The posting amount.</param>
    /// <returns>The sign-derived <see cref="BankPostingType"/>.</returns>
    public static BankPostingType EvaluateByAmountSign(decimal amount)
        => amount < 0m ? BankPostingType.Outflow : BankPostingType.Inflow;

    /// <summary>
    /// Resolves the effective posting type for a proposed value: a specified value is kept,
    /// <see cref="BankPostingType.Unspecified"/> falls back to the amount-sign classification.
    /// </summary>
    /// <param name="proposed">The already determined or imported posting type.</param>
    /// <param name="amount">The posting amount used for the sign fallback.</param>
    /// <returns><paramref name="proposed"/> when specified; otherwise the sign-derived type.</returns>
    public static BankPostingType Resolve(BankPostingType proposed, decimal amount)
        => proposed == BankPostingType.Unspecified ? EvaluateByAmountSign(amount) : proposed;
}
